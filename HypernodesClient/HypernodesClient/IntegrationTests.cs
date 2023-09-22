﻿using Microsoft.Extensions.Configuration;
using Xunit;

namespace Hyperthetical.Tests
{
	public class IntegrationTests : IDisposable
	{
		private readonly Client _client;
		private readonly User _user;
		private string TestFundingKey;

		public IntegrationTests () {
			var configuration = new ConfigurationBuilder()
				.AddJsonFile("secrets.json",optional: false)
			.Build();
			string TestFundingKey = configuration["HyperApiKey"];

			_client = new Client(TestFundingKey);
			_user = new User(_client);
		}

		public void Dispose () {
			// Cleanup logic if necessary.
			_user.StopPolling();
		}

		[Fact]
		public async Task Test_GetAvailableGraphsAsync_ShouldReturnGraphList () {
			var graphs = await _client.GetAvailableGraphsAsync(TestFundingKey,"Examples");
			Assert.NotNull(graphs);
			Assert.True(graphs.Count > 0);
		}

		[Fact]
		public async Task Test_SetAndRunGraphAsync_ShouldReturnValidResponse () {
			// Assuming we know one valid graphName and its username for testing
			string testGraphName = "Basic";
			string testGraphUsername = "Helpers";

			bool graphSet = await _user.SetGraphAsync(testGraphName, testGraphUsername);
			Assert.True(graphSet);

			List<string> inputConvo = new List<string> { "Hello" };
			string response = await _user.ChatAsync(inputConvo);

			Assert.False(string.IsNullOrEmpty(response));
		}
	}
}
