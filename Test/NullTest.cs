using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shared;
using Shared.CustomParser;

namespace Test;

public class NullTest
{
    [OneTimeSetUp]
    public void Setup()
    {
        SetupMapper.Instance.Init();
    }
    
    [Test]
    public void TestNullSupplied()
    {
        var mapper = RoboMapper.RoboMapper.GetMapper<A, B>();

        var b = mapper.Map((B)null);

        Assert.AreEqual(null, b);
    }

}