using System;
using NUnit.Framework;
using RedisGeo.ServiceInterface;
using RedisGeo.ServiceModel;
using ServiceStack.Testing;
using ServiceStack;

namespace RedisGeo.Tests
{
    [TestFixture]
    public class UnitTests
    {
        private readonly ServiceStackHost appHost;

        public UnitTests()
        {
            appHost = new BasicAppHost(typeof(RedisGeoServices).Assembly)
            {
                ConfigureContainer = container =>
                {
                    //Add your IoC dependencies here
                }
            }
            .Init();
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            appHost.Dispose();
        }
    }
}
