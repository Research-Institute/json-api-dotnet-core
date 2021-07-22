using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Xunit;

namespace TestBuildingBlocks
{
    public abstract class FakerContainer
    {
        protected static int GetFakerSeed()
        {
            // The goal here is to have stable data over multiple test runs, but at the same time different data per test case.

            MethodBase testMethod = GetTestMethod();
            string testName = testMethod.DeclaringType?.FullName + "." + testMethod.Name;

            return GetDeterministicHashCode(testName);
        }

        private static MethodBase GetTestMethod()
        {
            var stackTrace = new StackTrace();

            MethodBase testMethod = stackTrace.GetFrames().Select(stackFrame => stackFrame.GetMethod()).FirstOrDefault(IsTestMethod);

            if (testMethod == null)
            {
                // If called after the first await statement, the test method is no longer on the stack,
                // but has been replaced with the compiler-generated async/wait state machine.
                throw new InvalidOperationException("Fakers can only be used from within (the start of) a test method.");
            }

            return testMethod;
        }

        private static bool IsTestMethod(MethodBase method)
        {
            if (method == null)
            {
                return false;
            }

            return method.GetCustomAttribute(typeof(FactAttribute)) != null || method.GetCustomAttribute(typeof(TheoryAttribute)) != null;
        }

        private static int GetDeterministicHashCode(string source)
        {
            // https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int index = 0; index < source.Length; index += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ source[index];

                    if (index == source.Length - 1)
                    {
                        break;
                    }

                    hash2 = ((hash2 << 5) + hash2) ^ source[index + 1];
                }

                return hash1 + hash2 * 1566083941;
            }
        }
    }
}
