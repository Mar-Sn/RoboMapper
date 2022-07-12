using System;
using NUnit.Framework;
using Shared.CustomParser;

namespace Test;

public class DuplicateParserDeclare
{
    [OneTimeSetUp]
    public void Setup()
    {
        SetupMapper.Instance.Init();
    }

    [Test]
    public void TestAddingParserAgain()
    {
        try
        {
            RoboMapper.RoboMapper.Bind<MappedA, MappedB>(e =>
            {
                e.MapWith<NotMappedParser>(nameof(MappedA.NotMappedA), nameof(MappedB.NotMappedB));
            });
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);
        }
        Assert.Pass();
    }
}