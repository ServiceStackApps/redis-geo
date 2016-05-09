using System.Collections.Generic;
using ServiceStack;
using ServiceStack.Redis;

namespace RedisGeo.ServiceModel
{
    [Route("/georesults/{State}")]
    public class FindGeoResults : IReturn<List<RedisGeoResult>>
    {
        public string State { get; set; }
        public long? WithinKm { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
    }
}