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
            _httpClient.Timeout = TimeSpan.FromSeconds(2); // タイムアウトを短く設定
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
            var response = await _httpClient.PutAsJsonAsync($"api/members/{Uri.EscapeDataString(member.RosterName)}/{member.ExcelId}", member);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteMemberAsync(string studentNumber)
        {
            var response = await _httpClient.DeleteAsync($"api/members/{studentNumber}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<CheckInLog?> CheckInAsync(string rosterName, int excelId, int eventId)
        {
            var response = await _httpClient.PostAsJsonAsync("api/members/checkin", new { RosterName = rosterName, ExcelId = excelId, EventId = eventId });
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CheckInResponse>();
            return result?.Log;
        }

        public async Task UpdateCheckInStatusAsync(string rosterName, int excelId, int eventId, string status)
        {
            var response = await _httpClient.PostAsJsonAsync("api/members/status", new { RosterName = rosterName, ExcelId = excelId, EventId = eventId, Status = status });
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<CheckInLog>> GetCheckInLogsAsync(int eventId)
        {
            var result = await _httpClient.GetFromJsonAsync<List<CheckInLog>>($"api/events/{eventId}/logs");
            return result ?? new List<CheckInLog>();
        }

        public async Task<RosterConfig?> GetRosterConfigAsync(string rosterName)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<RosterConfig>($"api/rosterconfigs/{Uri.EscapeDataString(rosterName)}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpdateRosterConfigAsync(RosterConfig config)
        {
            var response = await _httpClient.PostAsJsonAsync("api/rosterconfigs", config);
            response.EnsureSuccessStatusCode();
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
