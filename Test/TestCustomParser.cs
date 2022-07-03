using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shared;
using Shared.CustomParser;

namespace Test;

public class TestCustomParser
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
        var mapper = RoboMapper.RoboMapper.GetMapper<MappedA, MappedB>();

        var b = mapper.Map(
            new MappedA
        {
            NotMappedA = new NotMappedA
            {
                CanMapThis = true
            }
        });

        Assert.AreEqual("True", b.NotMappedB.CanMapThis);
    }
    
    [Test]
    public void FromBToA()
    {
        var mapper = RoboMapper.RoboMapper.GetMapper<MappedB, MappedA>();

        var b = mapper.Map(
            new MappedA
            {
                NotMappedA = new NotMappedA
                {
                    CanMapThis = true
                }
            });

        Assert.AreEqual("True", b.NotMappedB.CanMapThis);
    }
    
    [Test]
    public void TestNull()
    {
        var mapper = RoboMapper.RoboMapper.GetMapper<MappedB, MappedA>();

        var b = mapper.Map(
            new MappedA
            {
                NotMappedA = new NotMappedA
                {
                    CanMapThis = null
                }
            });

        Assert.AreEqual(null, b.NotMappedB.CanMapThis);
    }
}