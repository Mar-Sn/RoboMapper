using NUnit.Framework;
using Shared.Inherentance;
using Shared.Inheritance;

namespace Test;

public class TestWithBaseClass
{
    [OneTimeSetUp]
    public void Setup()
    {
        SetupMapper.Instance.Init();
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