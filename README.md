# Redis GEO Example App

Redis GEO is a simple example showing how to make use of [Redis 3.2.0 new GEO API](http://antirez.com/news/104):

![](https://raw.githubusercontent.com/ServiceStack/Assets/master/img/livedemos/redis-geo/redisgeo-screenshot.png)

If Redis hasn't already cemented itself as the Swiss-Army-Knife server solution, 3.2.0 release has made it 
even more versatile and enhanced it with new [GEO capabilities](http://redis.io/commands/geoadd).

Aiming for the simplest possible useful demonstration, Redis GEO App lets you click on anywhere in the U.S. 
to find the list of nearest cities within a given radius. 

## Install Redis 3.2.0

In order to use the new GEO APIs you'll need the latest stable 3.2.0 release of redis which you can install
in your preferred *NIX system with:

    $ wget http://download.redis.io/releases/redis-3.2.0.tar.gz
    $ tar xzf redis-3.2.0.tar.gz
    $ cd redis-3.2.0
    $ make

## Create Project and Upgrade to v4.0.57+

Redis GEO was created with the 
[ServiceStack ASP.NET Empty](https://github.com/ServiceStack/ServiceStack/wiki/Creating-your-first-project)
project template but as the new GEO API's were just added you'll need to upgrade all ServiceStack packages
to the [pre-release v4.0.57 NuGet packages on MyGet](https://github.com/ServiceStack/ServiceStack/wiki/MyGet).

> This won't be necessary once the next **v4.0.58+** release of ServiceStack is published on NuGet

## Import Geonames dataset

To populate Redis with useful GEO data we'll import the 
[geonames.org postal data](http://download.geonames.org/export/zip/) which provides the zipcodes of all
US cities as well as their useful longitude and latitude coordinates.

The dataset is maintained in a tab-delimited `US.txt` text file which we do a fresh import of when your 
App first starts up:

```csharp
public class AppHost : AppHostBase
{
    public AppHost()
        : base("RedisGeo", typeof(RedisGeoServices).Assembly) {}

    public override void Configure(Container container)
    {
        JsConfig.EmitCamelCaseNames = true;

        container.Register<IRedisClientsManager>(c => 
            new RedisManagerPool(AppSettings.Get("RedisHost", defaultValue:"localhost")));

        ImportCountry(container.Resolve<IRedisClientsManager>(), "US");
    }

    public static void ImportCountry(IRedisClientsManager redisManager, string countryCode)
    {
        using (var redis = redisManager.GetClient())
        using (var reader = new StreamReader(
            File.OpenRead("~/App_Data/{0}.txt".Fmt(countryCode).MapHostAbsolutePath())))
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
```

This just parses the `US.txt` file in our Web Applications 
[/App_Data](https://github.com/ServiceStackApps/redis-geo/tree/master/src/RedisGeo/RedisGeo/App_Data) 
folder and extracts the **state** which we'll use as the key for our Redis GEO sorted set and populate 
it with the **longitude** and **latitude** of each **city**, skipping any duplicates. The script also 
imports the dataset for each state in separate batches using 
[GEOADD](http://redis.io/commands/geoadd) multi argument API.

### Implement the FindGeoResults Service

Our App only needs a single Service which we define the contract with using the 
[FindGeoResults](https://github.com/ServiceStackApps/redis-geo/blob/master/src/RedisGeo/RedisGeo.ServiceModel/FindGeoResults.cs)
Request DTO:

```csharp
[Route("/georesults/{State}")]
public class FindGeoResults : IReturn<List<RedisGeoResult>>
{
    public string State { get; set; }
    public long? WithinKm { get; set; }
    public double Lng { get; set; }
    public double Lat { get; set; }
}
```

That's the only DTO our App needs which returns a `List<RedisGeoResult>`. Implementing our Service is then 
just a matter fulfilling the above contract by delegating our populated Request DTO properties to the
`IRedisClient.FindGeoResultsInRadius()` API which itself just calls 
[GEORADIUS](http://redis.io/commands/georadius) and returns the results:

```csharp
public class RedisGeoServices : Service
{
    public object Any(FindGeoResults request)
    {
        var results = Redis.FindGeoResultsInRadius(request.State, 
            longitude: request.Lng, latitude: request.Lat,
            radius: request.WithinKm.GetValueOrDefault(10), unit: RedisGeoUnit.Kilometers,
            sortByNearest: true);

        return results;
    }
}
```

## Implement the Client

The entire client App is implemented in the static
[default.html](https://github.com/ServiceStackApps/redis-geo/blob/master/src/RedisGeo/RedisGeo/default.html)
which is just a jQuery App that only consists of the following markup:

```html
<div id="sidebar">
    <div class="inner">
        <h3>Redis GEO Example</h3>

        <div id="instructions">
            Click on Map to find nearest cities using
            <a href="http://redis.io/commands/georadius">Redis GEO</a>
        </div>

        <div id="info">
            Find cities in <b id="state"></b> within <input id="km" type="text" value="10" /> km
        </div>

        <ol id="results"></ol>
    </div>
</div>
<div id="map"></div>
```

To show our results from our **GEORADIUS** query and a `<div id="map"/>` placeholder used by
[Google Maps JavaScript API](https://developers.google.com/maps/documentation/javascript/) to render a 
our interactive map of the US in. 

The JavaScript just listens to every click on the map then uses the `Geocoder` API to find out which state 
the user clicked on at which point it adds a custom `Marker` and a `Circle` with the radius that's specified 
in the distance km textbox. 

It then calls our `/georesults/{State}` Service with the Lat/Lng of where the user clicked as well as the 
distance that it should search within, then displays all the cities within that radius in the Sidebar:

```js
var map;
function initMap() {
    map = new google.maps.Map(document.getElementById('map'), {
        center: { lat: 37.09024, lng: -95.7128917 },
        zoom: 5
    });
    var geocoder = new google.maps.Geocoder();
    var lastMarker, lastRadius;

    google.maps.event.addListener(map, "click", function(e) {
        geocoder.geocode({ 'location': e.latLng }, function(results, status) {
            if (status === google.maps.GeocoderStatus.OK) {
                map.setCenter(e.latLng);

                if (lastMarker != null)
                    lastMarker.setMap(null);

                var marker = lastMarker = new google.maps.Marker({
                    map: map,
                    position: e.latLng
                });

                if (lastRadius != null)
                    lastRadius.setMap(null);

                var km = parseInt($("#km").val());
                var radius = lastRadius = new google.maps.Circle({
                    strokeColor: "#c3fc49",
                    strokeOpacity: 0.8,
                    strokeWeight: 2,
                    fillColor: "#c3fc49",
                    fillOpacity: 0.35,
                    map: map,
                    center: e.latLng,
                    radius: km * 1000
                });
                radius.bindTo('center', marker, 'position');

                var state = getStateAbbr(results);
                $("#state").html(state);
                $("#instructions").hide();
                $("#info").show();

                $.getJSON("/georesults/" + state,
                    { lat: e.latLng.lat(), lng: e.latLng.lng(), withinKm: km },
                    function (r) {
                        var html = $.map(r, function(x) {
                            return "<li>" + x.member + " (" + x.distance.toFixed(2) + "km)</li>";
                        }).join('');
                        $("#results").html(html);
                    });
            }});
        });

    function getStateAbbr(results) {
        for (var i = 0; i < results.length; i++) {
            for (var j = 0; j < results[i].address_components.length; j++) {
                var addr = results[i].address_components[j];
                if (addr.types.indexOf("administrative_area_level_1") >= 0)
                    return addr.short_name;
            }
        }
        return null;
    }
}
```

The result is a quick demonstration where the user can click on anywhere in the U.S. to return the nearest 
points of interest. We hope this simple example piques your interest in Redis new GEO features and highlights
some potential use-cases possible with these new capabilities.

## Importing different country datasets

Whilst this example just imports US cities, you can change it to import your preferred country instead by
extracting the [Geonames](http://download.geonames.org/export/zip/) dataset and copying it into the 
[/App_Data](https://github.com/ServiceStackApps/redis-geo/tree/master/src/RedisGeo/RedisGeo/App_Data) 
folder then calling `ImportCountry()` with its country code.

E.g. we can import Ausrtalian Suburbs instead with:

```csharp
//ImportCountry(container.Resolve<IRedisClientsManager>(), "US");
ImportCountry(container.Resolve<IRedisClientsManager>(), "AU");
```

## ServiceStack.Redis GEO API's

Human friendly and convenient versions of each Redis GEO API is available in 
[IRedisClient](https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack.Interfaces/Redis/IRedisClient.cs)
below:

```csharp
public interface IRedisClient
{
    //...
    long AddGeoMember(string key, double longitude, double latitude, string member);
    long AddGeoMembers(string key, params RedisGeo[] geoPoints);
    double CalculateDistanceBetweenGeoMembers(string key, string fromMember, string toMember, string unit = null);
    string[] GetGeohashes(string key, params string[] members);
    List<RedisGeo> GetGeoCoordinates(string key, params string[] members);
    string[] FindGeoMembersInRadius(string key, double longitude, double latitude, double radius, string unit);
    List<RedisGeoResult> FindGeoResultsInRadius(string key, double longitude, double latitude, double radius, string unit, int? count = null, bool? sortByNearest = null);
    string[] FindGeoMembersInRadius(string key, string member, double radius, string unit);
    List<RedisGeoResult> FindGeoResultsInRadius(string key, string member, double radius, string unit, int? count = null, bool? sortByNearest = null);
}
```

Whilst lower-level API's which map 1:1 with Redis server operations are available in 
[IRedisNativeClient](https://github.com/ServiceStack/ServiceStack/blob/master/src/ServiceStack.Interfaces/Redis/IRedisNativeClient.cs):

```csharp
public interface IRedisNativeClient
{
    //...
    long GeoAdd(string key, double longitude, double latitude, string member);
    long GeoAdd(string key, params RedisGeo[] geoPoints);
    double GeoDist(string key, string fromMember, string toMember, string unit = null);
    string[] GeoHash(string key, params string[] members);
    List<RedisGeo> GeoPos(string key, params string[] members);
    List<RedisGeoResult> GeoRadius(string key, double longitude, double latitude, double radius, string unit,
        bool withCoords = false, bool withDist = false, bool withHash = false, int? count = null, bool? asc = null);
    List<RedisGeoResult> GeoRadiusByMember(string key, string member, double radius, string unit,
        bool withCoords = false, bool withDist = false, bool withHash = false, int? count = null, bool? asc = null);
}
```
