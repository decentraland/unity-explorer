using System;

namespace DCL.EventsApi
{
    [Serializable]
    public class EventWithPlaceIdDTO : IEventDTO
    {
        public string place_id;
        public string id;
        public string name;
        public string image;
        public string description;
        public string next_start_at;
        public string next_finish_at;
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
        public int x;
        public int y;

        public string Id => id;
        public string Name => name;
        public string Image => image;
        public string Description => description;
        public string Next_start_at => next_start_at;
        public string Next_finish_at => next_finish_at;
        public string Finish_at => finish_at;
        public string Scene_name => scene_name;
        public int[] Coordinates => coordinates;
        public string Server => server;
        public int Total_attendees => total_attendees;
        public bool Live => live;
        public string User_name => user_name;
        public bool Highlighted => highlighted;
        public bool Trending => trending;
        public bool Attending => attending;
        public string[] Categories => categories;
        public bool Recurrent => recurrent;
        public double Duration => duration;
        public string Start_at => start_at;
        public string[] Recurrent_dates => recurrent_dates;
        public bool World => world;
        public int X => x;
        public int Y => y;
    }
}
