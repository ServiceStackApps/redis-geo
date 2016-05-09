using System.Collections.Generic;
using ServiceStack;
using ServiceStack.Redis;

namespace RedisGeo.ServiceModel
{
    [Route("/georesults/{State}")]
    public class GetGeoResults : IReturn<GeoResultsResponse>
    {
        public string State { get; set; }
        public long? WithinKm { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
    }

    public class GeoResultsResponse
    {
        public List<RedisGeoResult> Results { get; set; }
    }
}