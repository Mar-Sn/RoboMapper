using NUnit.Framework;

namespace Test
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
         
        }
        
        
        [Test]
        public void FromAToB()
        {
            var mapper = RoboMapper.RoboMapper.GetMapper<A, B>();

            var b = mapper.Map(new A {Id1 = 7698});

            Assert.AreEqual(7698, b.Id2);
        }


        [Test]
        public void FromBToA()
        {
            var mapper = RoboMapper.RoboMapper.GetMapper<A, B>();

            var a = mapper.Map(new B { Id2 = 7698 });

            Assert.AreEqual(7698, a.Id1);
        }


        [Test]
        public void FromBToC()
        {
            var mapper = RoboMapper.RoboMapper.GetMapper<B, C>();

            var c = mapper.Map(new B { Id2 = 7698 });

            Assert.AreEqual(7698, c.Id3);
        }


        [Test]
        public void FromAToBToC()
        {
            var mapperAtoB = RoboMapper.RoboMapper.GetMapper<A, B>();
            var mapperBtoC = RoboMapper.RoboMapper.GetMapper<B, C>();

            var b = mapperAtoB.Map(new A { Id1 = 7698 });
            var c = mapperBtoC.Map(b);

            Assert.AreEqual(7698, c.Id3);
        }


        [Test]
        public void WithClasses()
        {
            var mapper = RoboMapper.RoboMapper.GetMapper<D, E>();

            var d = mapper.Map(new E
            {
                B1 = new B
                {
                    Id2 = 7698
                }
            });

            Assert.AreEqual(7698, d.A.Id1);
        }

        [Test]
        public void TestIgnore()
        {
            var mapper = RoboMapper.RoboMapper.GetMapper<Ignore1, Ignore2>();

            var test = mapper.Map(new Ignore2
            {
                Test1 = "test1"
            });
            
            Assert.AreEqual("test1", test.Test1);
            Assert.AreEqual(null, test.Test2);
        }
    }
}