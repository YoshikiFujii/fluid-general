using fluid_general.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace fluid_general.Services
{
    public class RemoteDataService : IDataService
    {
        private readonly HttpClient _httpClient;

        public RemoteDataService()
        {
            _httpClient = new HttpClient();
            // App.ServerBaseUrlは必ずスラッシュで終わる前提
            if (App.ServerBaseUrl != null)
            {
                _httpClient.BaseAddress = new Uri(App.ServerBaseUrl);
            }
        }

        public async Task<List<EventConfig>> GetEventsAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<EventConfig>>("api/events");
            return result ?? new List<EventConfig>();
        }

        public async Task<EventConfig?> GetEventAsync(int id)
        {
            return await _httpClient.GetFromJsonAsync<EventConfig>($"api/events/{id}");
        }

        public async Task<EventConfig> CreateEventAsync(EventConfig eventConfig)
        {
            var response = await _httpClient.PostAsJsonAsync("api/events", eventConfig);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<EventConfig>();
            return created ?? eventConfig;
        }

        public async Task UpdateEventAsync(EventConfig eventConfig)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/events/{eventConfig.Id}", eventConfig);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteEventAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"api/events/{id}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<Member>> GetMembersAsync()
        {
            var result = await _httpClient.GetFromJsonAsync<List<Member>>("api/members");
            return result ?? new List<Member>();
        }

        public async Task<List<Member>> GetMembersByRosterAsync(string rosterName)
        {
            var result = await _httpClient.GetFromJsonAsync<List<Member>>($"api/members/roster/{rosterName}");
            return result ?? new List<Member>();
        }

        public async Task<Member?> GetMemberAsync(string studentNumber)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<Member>($"api/members/{studentNumber}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<Member> CreateMemberAsync(Member member)
        {
            var response = await _httpClient.PostAsJsonAsync("api/members", member);
            response.EnsureSuccessStatusCode();
            var created = await response.Content.ReadFromJsonAsync<Member>();
            return created ?? member;
        }

        public async Task UpdateMemberAsync(Member member)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/members/{member.Id}", member);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteMemberAsync(string studentNumber)
        {
            var response = await _httpClient.DeleteAsync($"api/members/{studentNumber}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<CheckInLog?> CheckInAsync(string studentNumber, int eventId)
        {
            var request = new { StudentNumber = studentNumber, EventId = eventId };
            var response = await _httpClient.PostAsJsonAsync("api/members/checkin", request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CheckInResponse>();
                return result?.Log;
            }
            return null;
        }

        public async Task UpdateCheckInStatusAsync(string studentNumber, int eventId, string status)
        {
            var request = new { StudentNumber = studentNumber, EventId = eventId, Status = status };
            await _httpClient.PostAsJsonAsync("api/members/status", request);
        }

        public async Task<List<CheckInLog>> GetCheckInLogsAsync(int eventId)
        {
            var result = await _httpClient.GetFromJsonAsync<List<CheckInLog>>($"api/events/{eventId}/logs");
            return result ?? new List<CheckInLog>();
        }
    }

    // JSONレスポンス受け取り用の内部クラス
    internal class CheckInResponse
    {
        public string Message { get; set; } = string.Empty;
        public Member Member { get; set; } = null!;
        public CheckInLog Log { get; set; } = null!;
    }
}
