using System;
using NUnit.Framework;
using Shared.CustomParser;

namespace Test;

public class ParserPointToNonExistingField
{
    [OneTimeSetUp]
    public void Setup()
    {
        SetupMapper.Instance.Init();
    }

    [Test]
    public void ExpectFailureOnNonExistingFields()
    {
        try
        {
            RoboMapper.RoboMapper.Bind<MappedA, MappedB>(e =>
            {
                e.MapWith<NotMappedParser>("NonExistingField1", "NonExistingField2");
            });
            Assert.Fail();
        }
        catch (Exception e)
        {
            Assert.Pass();
        }
    }
}