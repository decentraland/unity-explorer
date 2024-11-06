using System;

namespace DCL.EventsApi
{
    [Serializable]
    public struct EventDTO
    {
        public string id;
        public string name;
        public string image;
        public string description;
        public string next_start_at;
        public string finish_at;
        public string scene_name;
        public int[] coordinates;
        public string server;
        public int total_attendees;
        public bool live;
        public string user_name;
        public bool highlighted;
        public bool trending;
        public bool attending;
        public string[] categories;
        public bool recurrent;
        public double duration;
        public string start_at;
        public string[] recurrent_dates;
        public bool world;
    }
}
