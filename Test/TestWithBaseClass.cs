using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shared;
using Shared.Inherentance;

namespace Test;

public class TestWithBaseClass
{
    [OneTimeSetUp]
    public void Setup()
    {
        var logger = new LoggerFactory();
        RoboMapper.RoboMapper.Define<A>();
        RoboMapper.RoboMapper.Init(logger.CreateLogger(nameof(TestSimpleConversion)));
    }

    [Test]
    public void FromAToB()
    {
        var mapper = RoboMapper.RoboMapper.GetMapper<AWithBase, BWithBase>();

        var b = mapper.Map(new AWithBase { Field = 7698, FieldA = 4533});

        Assert.AreEqual(7698, b.Field);
        Assert.AreEqual(4533, b.FieldB);
    }
    
    [Test]
    public void FromBToA()
    {
        var mapper = RoboMapper.RoboMapper.GetMapper<BWithBase, AWithBase>();

        var a = mapper.Map(new BWithBase { Field = 7698, FieldB = 4533});

        Assert.AreEqual(7698, a.Field);
        Assert.AreEqual(4533, a.FieldA);
    }
}