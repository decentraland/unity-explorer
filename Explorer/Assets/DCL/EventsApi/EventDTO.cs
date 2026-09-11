using System;
using UnityEngine;

namespace DCL.EventsApi
{
    public interface IEventDTO : ISerializationCallbackReceiver
    {
        string Id {get; set; }
        string Name {get; set; }
        string Image {get; set; }
        string Description {get; set; }
        string NextStartAt {get; set; }
        DateTime NextStartAtProcessed {get; set; }
        string NexFinishAt {get; set; }
        string FinishAt {get; set; }
        string SceneName {get; set; }
        int[] Coordinates {get; set; }
        string Server {get; set; }
        int TotalAttendees {get; set; }
        bool Live {get; set; }
        string UserName {get; set; }
        bool Highlighted {get; set; }
        bool Trending {get; set; }
        bool Attending {get; set; }
        string[] Categories {get; set; }
        bool Recurrent {get; set; }
        double Duration {get; set; }
        string StartAt {get; set; }
        DateTime StartAtProcessed {get; set; }
        string[] RecurrentDates {get; set; }
        DateTime[] RecurrentDatesProcessed {get; set; }
        bool World {get; set; }
        int X {get; set; }
        int Y {get; set; }
    }

    [Serializable]
    public struct EventDTO : IEventDTO
    {
        // ReSharper disable InconsistentNaming
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
        public string place_id;
        public string[] connected_addresses;
        public string community_id;
        public string image_vertical;
        // ReSharper restore InconsistentNaming

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

        public string NextStartAt
        {
            get => next_start_at;
            set => next_start_at = value;
        }

        private DateTime nextStartAtProcessed;
        public DateTime NextStartAtProcessed
        {
            get => nextStartAtProcessed;
            set => nextStartAtProcessed = value;
        }

        public string NexFinishAt
        {
            get => next_finish_at;
            set => next_finish_at = value;
        }

        public string FinishAt
        {
            get => finish_at;
            set => finish_at = value;
        }

        public string SceneName
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

        public int TotalAttendees
        {
            get => total_attendees;
            set => total_attendees = value;
        }

        public bool Live
        {
            get => live;
            set => live = value;
        }

        public string UserName
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

        public string StartAt
        {
            get => start_at;
            set => start_at = value;
        }

        private DateTime startAtProcessed;
        public DateTime StartAtProcessed
        {
            get => startAtProcessed;
            set => startAtProcessed = value;
        }

        public string[] RecurrentDates
        {
            get => recurrent_dates;
            set => recurrent_dates = value;
        }

        private DateTime[] recurrentDatesProcessed;
        public DateTime[] RecurrentDatesProcessed{
            get => recurrentDatesProcessed;
            set => recurrentDatesProcessed = value;
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

        //No need to serialize anything more than the already present fields
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize() =>
            EventDataParser.ParseDeserializedDates(ref this);
    }
}
