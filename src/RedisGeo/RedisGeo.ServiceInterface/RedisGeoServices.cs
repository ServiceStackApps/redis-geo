using ServiceStack;
using RedisGeo.ServiceModel;
using ServiceStack.Redis;

namespace RedisGeo.ServiceInterface
{
    public class RedisGeoServices : Service
    {
        public object Any(GetGeoResults request)
        {
            var results = Redis.FindGeoResultsInRadius(request.State, 
                longitude: request.Lng, latitude: request.Lat,
                radius: request.WithinKm.GetValueOrDefault(10), unit: RedisGeoUnit.Kilometers,
                sortByNearest: true);

            return new GeoResultsResponse { Results = results };
        }
    }
}