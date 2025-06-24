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

        public string Id
        {
            get => id;
            set => id = value;
        }

        public string Name
        {
            get => name;
            set => name = value;
        }

        public string Image
        {
            get => image;
            set => image = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public string Next_start_at
        {
            get => next_start_at;
            set => next_start_at = value;
        }

        public string Next_finish_at
        {
            get => next_finish_at;
            set => next_finish_at = value;
        }

        public string Finish_at
        {
            get => finish_at;
            set => finish_at = value;
        }

        public string Scene_name
        {
            get => scene_name;
            set => scene_name = value;
        }

        public int[] Coordinates
        {
            get => coordinates;
            set => coordinates = value;
        }

        public string Server
        {
            get => server;
            set => server = value;
        }

        public int Total_attendees
        {
            get => total_attendees;
            set => total_attendees = value;
        }

        public bool Live
        {
            get => live;
            set => live = value;
        }

        public string User_name
        {
            get => user_name;
            set => user_name = value;
        }

        public bool Highlighted
        {
            get => highlighted;
            set => highlighted = value;
        }

        public bool Trending
        {
            get => trending;
            set => trending = value;
        }

        public bool Attending
        {
            get => attending;
            set => attending = value;
        }

        public string[] Categories
        {
            get => categories;
            set => categories = value;
        }

        public bool Recurrent
        {
            get => recurrent;
            set => recurrent = value;
        }

        public double Duration
        {
            get => duration;
            set => duration = value;
        }

        public string Start_at
        {
            get => start_at;
            set => start_at = value;
        }

        public string[] Recurrent_dates
        {
            get => recurrent_dates;
            set => recurrent_dates = value;
        }

        public bool World
        {
            get => world;
            set => world = value;
        }

        public int X
        {
            get => x;
            set => x = value;
        }

        public int Y
        {
            get => y;
            set => y = value;
        }
    }
}
