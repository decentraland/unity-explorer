using System;

namespace DCL.EventsApi
{
    public interface IEventDTO
    {
        public string id { get; set; }
        public string name { get; set; }
        public string image { get; set; }
        public string description { get; set; }
        public string next_start_at { get; set; }
        public string next_finish_at { get; set; }
        public string finish_at { get; set; }
        public string scene_name { get; set; }
        public int[] coordinates { get; set; }
        public string server { get; set; }
        public int total_attendees { get; set; }
        public bool live { get; set; }
        public string user_name { get; set; }
        public bool highlighted { get; set; }
        public bool trending { get; set; }
        public bool attending { get; set; }
        public string[] categories { get; set; }
        public bool recurrent { get; set; }
        public double duration { get; set; }
        public string start_at { get; set; }
        public string[] recurrent_dates { get; set; }
        public bool world { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }

    [Serializable]
    public struct EventDTO : IEventDTO
    {
        public string id { get; set; }
        public string name { get; set; }
        public string image { get; set; }
        public string description { get; set; }
        public string next_start_at { get; set; }
        public string next_finish_at { get; set; }
        public string finish_at { get; set; }
        public string scene_name { get; set; }
        public int[] coordinates { get; set; }
        public string server { get; set; }
        public int total_attendees { get; set; }
        public bool live { get; set; }
        public string user_name { get; set; }
        public bool highlighted { get; set; }
        public bool trending { get; set; }
        public bool attending { get; set; }
        public string[] categories { get; set; }
        public bool recurrent { get; set; }
        public double duration { get; set; }
        public string start_at { get; set; }
        public string[] recurrent_dates { get; set; }
        public bool world { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }
}
