using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceMissionControl
{
    public class WeatherData
    {
        public string Location { get; set; }
        public List <WeatherDay> WeatherByDay { get; set; } = new List<WeatherDay> ();
    }

    public class WeatherDay
    {
        public int Day { get; set; }
        public int Temperature { get; set; }
        public int Wind { get; set; }
        public int Humidity { get; set;}
        public int Precipitation { get; set; }
        public bool Lightning { get; set; }
        public string Clouds { get; set; }
        public string Location { get; set; }
    }
}
