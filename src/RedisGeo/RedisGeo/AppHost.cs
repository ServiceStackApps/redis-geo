using System.Collections.Generic;
using System.IO;
using Funq;
using ServiceStack;
using RedisGeo.ServiceInterface;
using ServiceStack.Redis;
using ServiceStack.Text;

namespace RedisGeo
{
    public class AppHost : AppHostBase
    {
        /// <summary>
        /// Default constructor.
        /// Base constructor requires a name and assembly to locate web service classes. 
        /// </summary>
        public AppHost()
            : base("RedisGeo", typeof(RedisGeoServices).Assembly) {}

        public override void Configure(Container container)
        {
            JsConfig.EmitCamelCaseNames = true;

            //Requires GEO commands in v4.0.57 pre-release packages from MyGet: https://github.com/ServiceStack/ServiceStack/wiki/MyGet
            container.Register<IRedisClientsManager>(c => 
                new RedisManagerPool(AppSettings.Get("RedisHost", defaultValue:"localhost:6379")));

            ImportCountry(container.Resolve<IRedisClientsManager>(), "US");
        }

        public static void ImportCountry(IRedisClientsManager redisManager, string countryCode)
        {
            using (var redis = redisManager.GetClient())
            using (var reader = new StreamReader(File.OpenRead("~/App_Data/{0}.txt".Fmt(countryCode).MapHostAbsolutePath())))
            {
                string line, lastState = null, lastCity = null;
                var results = new List<ServiceStack.Redis.RedisGeo>();
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split('\t');
                    var city = parts[2];
                    var state = parts[4];
                    var latitude = double.Parse(parts[9]);
                    var longitude = double.Parse(parts[10]);

                    if (city == lastCity) //Skip duplicate entries
                        continue;
                    else
                        lastCity = city;

                    if (lastState == null)
                        lastState = state;

                    if (state != lastState)
                    {
                        redis.AddGeoMembers(lastState, results.ToArray());
                        lastState = state;
                        results.Clear();
                    }

                    results.Add(new ServiceStack.Redis.RedisGeo(longitude, latitude, city));
                }
            }
        }
    }
}