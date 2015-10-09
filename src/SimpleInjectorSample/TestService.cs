using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleInjectorSample
{
    public interface ITestService
    {
        int Add(int x, int y);
    }

    public class TestService : ITestService
    {
        public int Add(int x, int y)
        {
            return x + y;
        }
    }
}