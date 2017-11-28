using DotNetTestkit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Proxies;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using ImpromptuInterface;
using CassiniDev.Misc;

namespace CassiniDev.Tests
{
    [TestFixture]
    public class CrossDomainProxyIT
    {
        SolutionFiles solutionFiles = SolutionFiles.FromSolutionRoot();

        [Test]
        public void CreateADynamicProxy()
        {
            var invoker = Proxies.DynamicProxyFor<IGreeter>((method, args) =>
            {
                if (method.Name != "Hello")
                {
                    return "Invalid method";
                }

                return string.Format("Hello, {0}", args[0]);
            });

            Assert.That(invoker.Hello("Mantas"), Is.EqualTo("Hello, Mantas"));
        }

        [Test]
        public void CallAGreeter()
        {
            var binPath = solutionFiles.ResolvePath("Tests/ExampleApps/SetUpEnvironmentApp/bin/");
            var appDomain = CreateAppDomainFor(binPath);
            var proxy = new CrossDomainProxy(appDomain);

            var greeter = new Greeter();

            proxy.RegisterProxy<IGreeter>("A.B.C.Greeter", greeter);

            var result = proxy.InvokerFor("A.B.C.Greeter").Invoke("Hello", new object[] { "Mantas" });

            Assert.That(result, Is.EqualTo("Hello, Mantas"));
        }

        [Test]
        public void CallAComplexContract()
        {
            var binPath = solutionFiles.ResolvePath("Tests/ExampleApps/SetUpEnvironmentApp/bin/");
            var appDomain = CreateAppDomainFor(binPath);
            var proxy = new CrossDomainProxy(appDomain);

            var greeter = new ComplexGreeter();

            proxy.RegisterProxy<IComplexGreeter>("A.B.C.Greeter", greeter);

            var result = (Dictionary<string, object>) proxy.InvokeInDomain("A.B.C.Greeter", "Hello", new object[] {
                new Dictionary<string, object> { { "Name", "Mantas" } }
            });

            Assert.That(result["Greeting"], Is.EqualTo("Hello, Mantas"));
        }

        private static AppDomain CreateAppDomainFor(string dllPath)
        {
            var curDomain = AppDomain.CurrentDomain;
            var binPath = Path.GetDirectoryName(dllPath);
            //var shadowBin = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            //Directory.CreateDirectory(shadowBin);
            //File.WriteAllText(Path.Combine(shadowBin, "hello.txt"), "Hello");

            //Console.WriteLine("Shadow bin at {0}", shadowBin);

            var evidence = new Evidence(AppDomain.CurrentDomain.Evidence);

            //var setup = new AppDomainSetup();
            var name = "DomainUnderTest";

            //setup.ApplicationName = name;
            //setup.DynamicBase = curDomain.DynamicDirectory;
            //setup.CachePath = shadowBin;
            //setup.ShadowCopyDirectories = null;
            //setup.ShadowCopyFiles = "true";
            //setup.ApplicationBase = binPath;
            ////setup.se

            ////setup.ConfigurationFile = Path.Combine(dirPath, configFile);
            //setup.PrivateBinPath = binPath;

            var domain = AppDomain.CreateDomain(name, evidence, binPath, null, true);

            return domain;
        }
    }

    public class CrossDomainProxy: MarshalByRefObject
    {
        private readonly AppDomain domain;

        private InDomainHost inDomainHost;

        public CrossDomainProxy(AppDomain domain)
        {
            this.domain = domain;

            var proxyObjectHandle = domain.CreateInstanceFrom(Assembly.GetExecutingAssembly().Location, typeof(InDomainHost).FullName);

            inDomainHost = (InDomainHost)proxyObjectHandle.Unwrap();
        }

        public T RegisterProxy<T>(string typeName, T implementation) where T: class
        {
            var callerProxy = new CallerProxy(implementation);

            inDomainHost.RegisterProxy(typeName, callerProxy);

            return callerProxy.InvocationHandlerFor<T>();
        }

        public IDynamicInvoker InvokerFor(string typeName)
        {
            return inDomainHost.InvokerFor(typeName);
        }

        public object InvokeInDomain(string typeName, string method, object[] args)
        {
            return inDomainHost.Invoke(typeName, method, args);
        }
    }

    public class InDomainHost : MarshalByRefObject
    {
        private Dictionary<string, IDynamicInvoker> invokers = new Dictionary<string, IDynamicInvoker>();

        public void RegisterProxy(string typeName, CallerProxy proxy)
        {
            invokers.Add(typeName, proxy);
        }

        public IDynamicInvoker InvokerFor(string typeName)
        {
            return invokers[typeName];
        }

        public object Invoke(string typeName, string method, object[] args)
        {
            return invokers[typeName].Invoke(method, args);
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }

    public interface IDynamicInvoker
    {
        object Invoke(string methodName, object[] args);
    }

    public class CallerProxy: MarshalByRefObject, IDynamicInvoker, IConvertible
    {
        private object implementation;

        public CallerProxy(object implementation)
        {
            this.implementation = implementation;
        }

        public object Invoke(string methodName, object[] args)
        {
            var method = implementation.GetType().GetMethod(methodName);

            return AdaptReturnType(method, method.Invoke(implementation, AdaptArgs(method, args)));
        }

        public T InvocationHandlerFor<T>() where T: class
        {
            return Proxies.DynamicProxyFor<T>((method, args) =>
            {
                return Invoke(method.Name, args);
            });
        }

        private object[] AdaptArgs(MethodInfo method, object[] rawArgs)
        {
            var methodParams = method.GetParameters();

            for (var i = 0; i < methodParams.Length; i++)
            {
                var paramType = methodParams[i];
                var value = rawArgs[i];

                if (value is Dictionary<string, object>)
                {
                    rawArgs[i] = AdaptType(paramType.ParameterType, value);
                }
            }

            return rawArgs;
        }

        private object AdaptType(Type parameterType, object value)
        {
            var result = Activator.CreateInstance(parameterType);

            if (value is Dictionary<string, object>)
            {
                foreach (var pair in value as Dictionary<string, object>)
                {
                    var prop = parameterType.GetProperty(pair.Key);

                    prop.SetValue(result, pair.Value);
                }
            }

            return result;
        }

        private object AdaptReturnType(MethodInfo method, object rawReturnValue)
        {
            if (method.ReturnType == typeof(string))
            {
                return rawReturnValue;
            }

            if (method.ReturnType.IsClass)
            {
                return rawReturnValue.ToDictionary();
            }

            return rawReturnValue;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return InvalidType<bool>();
        }

        public byte ToByte(IFormatProvider provider)
        {
            return InvalidType<byte>();
        }

        public char ToChar(IFormatProvider provider)
        {
            return InvalidType<char>();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return InvalidType<DateTime>();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return InvalidType<decimal>();
        }

        public double ToDouble(IFormatProvider provider)
        {
            return InvalidType<double>();
        }

        public short ToInt16(IFormatProvider provider)
        {
            return InvalidType<short>();
        }

        public int ToInt32(IFormatProvider provider)
        {
            return InvalidType<int>();
        }

        public long ToInt64(IFormatProvider provider)
        {
            return InvalidType<long>();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return InvalidType<sbyte>();
        }

        public float ToSingle(IFormatProvider provider)
        {
            return InvalidType<float>();
        }

        public string ToString(IFormatProvider provider)
        {
            return this.ToString();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return this;
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return InvalidType<ushort>();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return InvalidType<uint>();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return InvalidType<ulong>();
        }

        private T InvalidType<T>()
        {
            throw new InconvertibleType(typeof(T));
        }
    }


    public class Proxies
    {
        public static T DynamicProxyFor<T>(Func<MethodInfo, object[], object> handleInvoke) where T : class
        {
            var wrapper = new DynamicInvokeProxy<T>(handleInvoke);

            return wrapper.ActLike<T>();
        }
    }

    public class DynamicInvokeProxy<T> : DynamicObject where T : class
    {
        private readonly Type iface;
        private readonly Func<MethodInfo, object[], object> handleInvoke;

        public DynamicInvokeProxy(Func<MethodInfo, object[], object> handleInvoke)
        {
            this.iface = typeof(T);
            this.handleInvoke = handleInvoke;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var method = iface.GetMethod(binder.Name);

            if (method != null)
            {
                result = handleInvoke(method, args);
                return true;
            }

            return base.TryInvokeMember(binder, args, out result);
        }
    }

    public interface IGreeter
    {
        string Hello(string name);
    }
    
    public class Greeter : IGreeter
    {
        public string Hello(string name)
        {
            return string.Format("Hello, {0}", name);
        }
    }

    public interface IComplexGreeter
    {
        HelloResponse Hello(HelloRequest request);
    }
    
    public class ComplexGreeter : IComplexGreeter
    {
        public HelloResponse Hello(HelloRequest request)
        {
            return new HelloResponse
            {
                Greeting = string.Format("Hello, {0}", request.Name)
            };
        }
    }

    public class HelloRequest
    {
        public string Name { get; set; }
    }

    public class HelloResponse
    {
        public string Greeting { get; set; }
    }
}
