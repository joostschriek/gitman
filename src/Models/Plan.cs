using System.Text.Json.Serialization;

namespace gitman.models
{
    public class Plan
    {
        public string Name { get; set; }
        [JsonPropertyName("filled_seats")]
        public int FilledSeats { get; set; }
        public int Seats { get; set; }
    }
}
