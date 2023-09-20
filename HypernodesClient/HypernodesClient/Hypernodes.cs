using System.Text;
using Newtonsoft.Json;

namespace Hyperthetical
{
	/// <summary>
	/// You should generally have one client in each application. A client has a base key and client functions. If you need different users
	/// to have different keys, you can use the user's key override variable. You should only have different clients if you need to have namespaces
	/// for your client functions. 
	/// </summary>
	public class Client
	{

		public  readonly string baseUrl = "https://localhost:7032";
		//public readonly string baseUrl = "https://www.hyperthetical.dev";
		private readonly Dictionary<string, Func<List<List<string>>,User,Task>> clientFunctions;
		public string FundingKey { get; }
		
		public Client () {
			clientFunctions = new Dictionary<string, Func<List<List<string>>, User, Task>> ();
		}

		public Client (string fundingKey) : this(){
			// Here, initialize any internals related to the secret key if needed
			FundingKey = fundingKey;
		}

		/// <summary>
		/// Setup a callback function to listen for whenever a ClientFunctionNode is called in an associated graph run.
		/// </summary>
		public void AddClientFunction (string functionName, Func<List<List<string>>, User, Task> function) {
			clientFunctions[functionName] = function;
		}

		internal void ExecuteClientFunction (string functionName, List<List<string>> args, User user) {
			if (clientFunctions.TryGetValue(functionName, out var action)) {
				action(args,user);
			}
			else {
				Console.Out.WriteLine($"Unknown function: {functionName}");
			}
		}

		/// <summary>
		/// Gets a listof availible graphs at the given username. If you just want to supply a funding key, it will return the graphs attached to that key's user. 
		/// </summary>
		public async Task<List<GraphNodeData>> GetAvailableGraphsAsync (string key, string? username = null) {

			using var httpClient = new HttpClient();
			var requestBody = new {
				Key = key,
				Username = username
			};

			var response = await httpClient.PostAsync($"{baseUrl}/getgraphs", new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"));

			response.EnsureSuccessStatusCode();

			string jsonResponse = await response.Content.ReadAsStringAsync();
			var graphsInfoList = JsonConvert.DeserializeObject<List<GraphNodeData>>(jsonResponse);

			return graphsInfoList;
		}

	}

	/// <summary>
	/// Each user interaction should be a separate User object. It keeps track of some user session data and what graph this particular user 
	/// is talking to. If you need custom data accesed from your client functions, you should inherit from User. 
	/// </summary>
	public class User
	{
		public readonly Client client;
		public string pollingGuid;
		public string dataKey;
		string? fundingKeyOverride;
		CancellationTokenSource cancellationTokenSource;
		public string? graph;
		public string? graphUsername;
		/// <summary>
		/// You can, if you wish, override the funding key at the user level so each User will use their own Hypernodes key.
		/// </summary>
		public string fundingKey {
			get => string.IsNullOrEmpty(fundingKeyOverride) ? client.FundingKey : fundingKeyOverride;
			set => fundingKeyOverride = value;
		}

		public User (Client client) {
			this.client = client;
			this.pollingGuid = Guid.NewGuid().ToString();
			this.dataKey = Guid.NewGuid().ToString();
			this.cancellationTokenSource = new CancellationTokenSource();

			StartPolling();
		}

		// Constructor with supplied pollingGuid and dataKey
		public User (Client client, string pollingGuid, string dataKey) {
			this.client = client;
			this.pollingGuid = pollingGuid;
			this.dataKey = dataKey;
			this.cancellationTokenSource = new CancellationTokenSource();

			StartPolling();
		}

		/// <summary>
		/// The polling GUID is a code that's used to poll for client functions. It's sent up in all RunGraph requests.
		/// </summary>
		public void SetPollingGuid (string newId) {
			this.pollingGuid = newId;
		}

		/// <summary>
		/// Set the datakey. The datakey is a "user session" variable that's sent as the second argument to a graph (second output from the input node)
		/// It's a secret code meant to be used in mailboxes. It will be randomized unless you set it manually.
		/// </summary>
		public void SetDataKey (string newKey) {
			this.dataKey = newKey;
		}

		async void StartPolling () {
			await PollForResultAsync();
		}

		/// <summary>
		/// Does this User object have all the info it needs to build an API request to Hypernodes? 
		/// </summary>
		/// <returns></returns>
		public bool ready () {
			return !string.IsNullOrEmpty(fundingKey) && !string.IsNullOrEmpty(graphUsername) && !string.IsNullOrEmpty(graph);
		}

		const int InitialDelayMilliseconds = 500; // Initial delay (0.5 seconds)
		const int MaxDelayMilliseconds = 60000;   // Max delay (1 minute)
		const double Multiplier = 1.5;            // Multiplier for delay
		int currentDelay = InitialDelayMilliseconds;


		async Task PollForResultAsync () {
			if (cancellationTokenSource.IsCancellationRequested) return;

			using var httpClient = new HttpClient();
			var requestBody = new {
				Guid = pollingGuid
			};

			var response = await httpClient.PostAsync($"{client.baseUrl}/pollresult", new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"));

			if (!response.IsSuccessStatusCode) {
				Console.Out.WriteLine("Error: Unable to reach the server. Received status code: " + response.StatusCode);

				await Task.Delay(currentDelay);  // Wait for the current delay
				currentDelay = (int)(currentDelay * Multiplier); // Increase the delay
				if (currentDelay > MaxDelayMilliseconds) {
					currentDelay = MaxDelayMilliseconds; // Don't exceed the max delay
				}

				await PollForResultAsync();
			}
			else {
				currentDelay = InitialDelayMilliseconds; // Reset the delay on success

				string jsonResponse = await response.Content.ReadAsStringAsync();
				ClientFunction clientFunction = JsonConvert.DeserializeObject<ClientFunction>(jsonResponse);

				if (clientFunction != null) {
					client.ExecuteClientFunction(clientFunction.Name, clientFunction.Args, this);

					if (!clientFunction.Finished) {
						await PollForResultAsync();
					}
				}
				else {
					await Task.Delay(currentDelay); // If the response is not as expected, use the delay again before retrying
					await PollForResultAsync();
				}
			}
		}

		public void StopPolling () {
			cancellationTokenSource.Cancel();
		}

		/// <summary>
		/// Runs a given graph. You manually control all inputs. Generally it's better to use ChatAsync because that uses the builtin data such as the graph this User is attached to, and the 
		/// DataKey. 
		/// </summary>
		/// <param name="graphName"></param>
		/// <param name="graphUsername"></param>
		/// <param name="inputs"></param>
		/// <returns></returns>
		public async Task<string> RunGraphAsync (string graphName, string graphUsername, Dictionary<string, List<string>> inputs) {
			using var httpClient = new HttpClient();
			var request = new RunGraphRequest {
				GraphName = graphName,
				GraphUserName = graphUsername,
				InputData = inputs,
				PollingGuid = this.pollingGuid,
				FundingKey = fundingKey
			};

			string jsonRequest = JsonConvert.SerializeObject(request);
			var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

			var response = await httpClient.PostAsync($"{client.baseUrl}/rungraph", content);

			response.EnsureSuccessStatusCode(); // Ensure the request was successful

			string jsonResponse = await response.Content.ReadAsStringAsync();

			// Deserialize the JSON response to the RunGraphResponse type
			RunGraphResponse? runGraphResponse = JsonConvert.DeserializeObject<RunGraphResponse>(jsonResponse);

			return runGraphResponse?.response[0] ?? "";
		}

		/// <summary>
		/// Sets the graph this User will call when you use ChatAsync.
		/// </summary>
		/// <param name="graphName">The name of the graph, set when saving it.</param>
		/// <param name="username">The username of the graph - the username of the last account that saved it.</param>
		/// <param name="key">Optional - the funding key to be billed for this User. If not set for this user, it will use the Client funding key.</param>
		/// <returns></returns>
		public async Task<bool> SetGraphAsync (string graphName, string username, string? key = null) {
			// Fetch available graphs
			if(key == null) {
				key = fundingKey;
			}
			else {
				fundingKey = key;
			}
			List<GraphNodeData> availableGraphs = await  client.GetAvailableGraphsAsync(key, username);

			// Check if the desired graph exists
			if (availableGraphs.Any(g => g.Name == graphName)) {
				this.graph = graphName;
				this.graphUsername = username;
				return true; // Graph exists and has been set
			}

			return false; // Graph doesn't exist
		}


		/// <summary>
		/// Sends the messages to the graph set with the call "setGraph()". Uses the standard chatbot graph input format of 
		/// 1: convo
		/// 2: dataKey
		/// 3+: other messages
		/// </summary>
		/// <param name="inputConvo">A list of previous messages in the converation. Like OpenAI API, message history must be sent every time in standard chatbot format.</param>
		/// <param name="otherInputs">Other inputs can be optionally included. They aren't included in the standard format, but you may extend the standard format on some graphs.</param>
		/// <returns></returns>
		public async Task<string> ChatAsync (List<string> inputConvo, List<List<string>>? otherInputs = null) {
			var inputs = new Dictionary<string, List<string>>
			{
			{ "0", inputConvo },
			{ "1", new List<string> { this.dataKey } }
		};
			if (otherInputs!=null) {
				int index = 2;
				foreach (List<string> input in otherInputs) {
					inputs.Add(index.ToString(), input);
					index++;
				}
			}

			return await RunGraphAsync(graph, graphUsername, inputs);
		}

		public async Task<List<GraphNodeData>> GetAvailableGraphsAsync () {
			return await client.GetAvailableGraphsAsync(fundingKey, graphUsername);
		}
	}

	public class ClientFunction
	{
		public string Name { get; set; }
		public List<List<string>> Args { get; set; }
		public bool Finished { get; set; }
	}

	public class RunGraphRequest
	{
		public string GraphName { get; set; }
		public string GraphUserName { get; set; }
		public string FundingKey { get; set; }
		public Dictionary<string, List<string>> InputData { get; set; }
		public string PollingGuid { get; set; }
	}

	public class GraphNodeData
	{
		public string Type { get; set; }
		public string Name { get; set; }
		public List<string> Inputs { get; set; } = new List<string>();
		public List<string> Outputs { get; set; } = new List<string>();
		public string Description { get; set; }
		public List<string> Tags { get; set; } = new List<string>();


	}
	public class KeyData
	{
		[JsonProperty("key")]
		public string Key { get; set; }
		[JsonProperty("tokenCount")]
		public int TokenCount { get; set; }
		[JsonProperty("username")]
		public string Username { get; set; }
		[JsonProperty("paymentLink")]
		public string PaymentLink { get; set; }
	}


	public class RunGraphResponse
	{
		public List<string> response { get; set; }
		public long cost { get; set; }
	}


}