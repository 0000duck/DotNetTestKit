//  **********************************************************************************
//  CassiniDev - http://cassinidev.codeplex.com
// 
//  Copyright (c) 2010 Sky Sanders. All rights reserved.
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  
//  This source code is subject to terms and conditions of the Microsoft Public
//  License (Ms-PL). A copy of the license can be found in the license.txt file
//  included in this distribution.
//  
//  You must not remove this notice, or any other, from this software.
//  
//  **********************************************************************************

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Linq;
using CassiniDev.Configuration;
using CassiniDev.ServerLog;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.X509;
using System.Collections;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Parameters;
using CassiniDev.Autodeploy;
using CassiniDev.Core;
//using Service;
//using System.Data.Objects;


#endregion

namespace CassiniDev
{
    ///<summary>
    ///</summary>
    [PermissionSet(SecurityAction.LinkDemand, Name = "Everything"),
     PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class Server : MarshalByRefObject, IDisposable
    {
        private readonly bool _useLogger;
        ///<summary>
        ///</summary>
        public List<string> Plugins = new List<string>();
        ///<summary>
        ///</summary>

        private readonly bool _disableDirectoryListing;

        private readonly string _hostName;

        private readonly IPAddress _ipAddress;

        private readonly object _lockObject;

        private readonly string _physicalPath;

        private readonly int _port;
        private readonly bool _requireAuthentication;
        //private readonly int _timeoutInterval;
        private readonly string _virtualPath;
        private bool _disposed;

        private IntPtr _processToken;

        private string _processUser;

        //private int _requestCount;

        private bool _shutdownInProgress;

        private Socket _socket;
        private Socket _managementSocket;

        private AppHosts _appHosts;

        //private Timer _timer;

        private string _appId;

        public event EventHandler<HostCreatedEventArgs> HostCreated;
        public event EventHandler<HostRemovedEventArgs> HostRemoved;

        ///<summary>
        ///</summary>
        public string AppId
        {
            get { return _appId; }
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        public Server(int port, string virtualPath, string physicalPath)
            : this(port, virtualPath, physicalPath, false, false)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="physicalPath"></param>
        public Server(int port, string physicalPath)
            : this(port, "/", physicalPath, IPAddress.Loopback)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="physicalPath"></param>
        public Server(string physicalPath)
            : this(CassiniNetworkUtils.GetAvailablePort(32768, 65535, IPAddress.Loopback, false), physicalPath)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        ///<param name="ipAddress"></param>
        ///<param name="hostName"></param>
        ///<param name="requireAuthentication"></param>
        public Server(int port, string virtualPath, string physicalPath, IPAddress ipAddress, string hostName,
                      bool requireAuthentication)
            : this(port, virtualPath, physicalPath, ipAddress, hostName, requireAuthentication, false)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        ///<param name="requireAuthentication"></param>
        public Server(int port, string virtualPath, string physicalPath, bool requireAuthentication)
            : this(port, virtualPath, physicalPath, requireAuthentication, false)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        ///<param name="ipAddress"></param>
        ///<param name="hostName"></param>
        public Server(int port, string virtualPath, string physicalPath, IPAddress ipAddress, string hostName)
            : this(port, virtualPath, physicalPath, ipAddress, hostName, false, false)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        ///<param name="ipAddress"></param>
        ///<param name="hostName"></param>
        ///<param name="requireAuthentication"></param>
        ///<param name="disableDirectoryListing"></param>
        public Server(int port, string virtualPath, string physicalPath, IPAddress ipAddress, string hostName,
                      bool requireAuthentication, bool disableDirectoryListing)
            : this(port, virtualPath, physicalPath, requireAuthentication, disableDirectoryListing)
        {
            _ipAddress = ipAddress;
            _hostName = hostName;
            //_timeoutInterval = timeout;
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        ///<param name="ipAddress"></param>
        public Server(int port, string virtualPath, string physicalPath, IPAddress ipAddress)
            : this(port, virtualPath, physicalPath, ipAddress, null, false, false)
        {
        }

        ///<summary>
        ///</summary>
        ///<param name="port"></param>
        ///<param name="virtualPath"></param>
        ///<param name="physicalPath"></param>
        ///<param name="requireAuthentication"></param>
        ///<param name="disableDirectoryListing"></param>
        public Server(int port, string virtualPath, string physicalPath, bool requireAuthentication,
                      bool disableDirectoryListing)
        {
            try
            {
                Assembly.ReflectionOnlyLoad("Common.Logging");
                _useLogger = true;
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
            }
            _ipAddress = IPAddress.Loopback;
            _requireAuthentication = requireAuthentication;
            _disableDirectoryListing = disableDirectoryListing;
            _lockObject = new object();
            _port = port;
            _virtualPath = virtualPath;
            _physicalPath = Path.GetFullPath(physicalPath);
            _physicalPath = _physicalPath.EndsWith("\\", StringComparison.Ordinal)
                                ? _physicalPath
                                : _physicalPath + "\\";
            ProcessConfiguration();

            string uniqueAppString = string.Concat(virtualPath, physicalPath, ":", _port.ToString()).ToLowerInvariant();
            _appId = (uniqueAppString.GetHashCode()).ToString("x", CultureInfo.InvariantCulture);
            ObtainProcessToken();

            _appHosts = new AppHosts(this, _virtualPath, _physicalPath);

            _appHosts.HostCreated += (s, e) =>
            {
                if (HostCreated != null)
                {
                    HostCreated.Invoke(s, e);
                }
            };

            _appHosts.HostRemoved += (s, e) =>
            {
                if (HostRemoved != null)
                {
                    HostRemoved.Invoke(s, e);
                }
            };

#if NET40
            //m_projMonitor = new HostWatchManager();
            //m_projMonitor.AddSolution(@"D:\Users\mantasi\Documents\Visual Studio 2010\Projects\Core\Core.sln");
#endif
        }

        private void ProcessConfiguration()
        {
            // #TODO: how to identify profile to use?
            // current method is to either use default '*' profile or match port
            // port can be an arbitrary value, especially in testing scenarios so
            // perhaps a regex based path matching strategy can also be offered

            var config = CassiniDevConfigurationSection.Instance;
            if (config != null)
            {
                foreach (CassiniDevProfileElement profile in config.Profiles)
                {
                    if (profile.Port == "*" || Convert.ToInt64(profile.Port) == _port)
                    {
                        foreach (PluginElement plugin in profile.Plugins)
                        {
                            Plugins.Insert(0, plugin.Type);
                        }
                    }
                }
            }
        }

        ///<summary>
        ///</summary>
        ///<param name="physicalPath"></param>
        ///<param name="requireAuthentication"></param>
        public Server(string physicalPath, bool requireAuthentication)
            : this(
                CassiniNetworkUtils.GetAvailablePort(32768, 65535, IPAddress.Loopback, false), "/", physicalPath,
                requireAuthentication)
        {
        }



        ///<summary>
        ///</summary>
        public bool DisableDirectoryListing
        {
            get { return _disableDirectoryListing; }
        }

        ///<summary>
        ///</summary>
        public bool RequireAuthentication
        {
            get { return _requireAuthentication; }
        }

        /////<summary>
        /////</summary>
        //public int TimeoutInterval
        //{
        //    get { return _timeoutInterval; }
        //}

        ///<summary>
        ///</summary>
        public string HostName
        {
            get { return _hostName; }
        }

        ///<summary>
        ///</summary>
        // ReSharper disable InconsistentNaming
        public IPAddress IPAddress
        // ReSharper restore InconsistentNaming
        {
            get { return _ipAddress; }
        }

        ///<summary>
        ///</summary>
        public string PhysicalPath
        {
            get { return _physicalPath; }
        }

        ///<summary>
        ///</summary>
        public int Port
        {
            get { return _port; }
        }

        ///<summary>
        ///</summary>
        public string RootUrl
        {
            get
            {
                string hostname = _hostName;
                if (string.IsNullOrEmpty(_hostName))
                {
                    if (_ipAddress.Equals(IPAddress.Loopback) || _ipAddress.Equals(IPAddress.IPv6Loopback) ||
                        _ipAddress.Equals(IPAddress.Any) || _ipAddress.Equals(IPAddress.IPv6Any))
                    {
                        hostname = "localhost";
                    }
                    else
                    {
                        hostname = _ipAddress.ToString();
                    }
                }

                return _port != 80
                           ?
                               String.Format("http://{0}:{1}{2}", hostname, _port, _virtualPath)
                           :
                    //FIX: #12017 - TODO:TEST
                       string.Format("http://{0}{1}", hostname, _virtualPath);
            }
        }

        ///<summary>
        ///</summary>
        public string VirtualPath
        {
            get { return _virtualPath; }
        }

        public TextWriter OutputWriter {
            get
            {
                return _appHosts.OutputWriter;
            }
            set
            {
                _appHosts.OutputWriter = value;
            }
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (!_disposed)
            {
                ShutDown();
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion

        ///<summary>
        ///</summary>
        public event EventHandler<RequestEventArgs> RequestComplete;

        /////<summary>
        /////</summary>
        //public event EventHandler TimedOut;

        ///<summary>
        ///</summary>
        ///<returns></returns>
        public IntPtr GetProcessToken()
        {
            return _processToken;
        }

        ///<summary>
        ///</summary>
        ///<returns></returns>
        public string GetProcessUser()
        {
            return _processUser;
        }

        ///<summary>
        ///</summary>
        public void HostStopped()
        {
            _appHosts = null;
        }

        /// <summary>
        /// Obtains a lifetime service object to control the lifetime policy for this instance.
        /// </summary>
        /// <returns>
        /// An object of type <see cref="T:System.Runtime.Remoting.Lifetime.ILease"/> used to control the lifetime policy for this instance. This is the current lifetime service object for this instance if one exists; otherwise, a new lifetime service object initialized to the value of the <see cref="P:System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseManagerPollTime"/> property.
        /// </returns>
        /// <exception cref="T:System.Security.SecurityException">The immediate caller does not have infrastructure permission. 
        ///                 </exception><filterpriority>2</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="RemotingConfiguration, Infrastructure"/></PermissionSet>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            // never expire the license
            return null;
        }

        // called at the end of request processing
        // to disconnect the remoting proxy for Connection object
        // and allow GC to pick it up
        /// <summary>
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="userName"></param>
        public void OnRequestEnd(Connection conn, string userName)
        {
            try
            {
                LogInfo connRequestLogClone = conn.RequestLog.Clone();
                connRequestLogClone.Identity = userName;
                LogInfo connResponseLogClone = conn.ResponseLog.Clone();
                connResponseLogClone.Identity = userName;
                OnRequestComplete(conn.Id, connRequestLogClone, connResponseLogClone);
            }
            catch
            {
                // swallow - we don't want consumer killing the server
            }
            RemotingServices.Disconnect(conn);
            //DecrementRequestCount();
        }

        private const int MaxHeaderBytes = 32 * 1024;
        private Dictionary<string, string> _virtualPaths;
        private List<string> _applicationPaths;
        private HostWatch _hostWatch;

        ///<summary>
        ///</summary>
        public void Start()
        {
            Start(_ => { });
        }

        ///<summary>
        ///</summary>
        public void Start(Action<ServerInfo> setupAction)
        {
            _managementSocket = CreateSocketBindAndListen(AddressFamily.InterNetwork, _ipAddress, 0);

            var serverInfo = new ServerInfo(((IPEndPoint)_managementSocket.LocalEndPoint).Port);

            try
            {
                setupAction(serverInfo);
            } catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            //_applicationPaths = _Host.GetApplicationVirtualPaths();

            //start the timer
            //DecrementRequestCount();

            //X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            //store.Open(OpenFlags.ReadOnly);

            //X509Certificate2 certificate = null;

            /*foreach (X509Certificate2 certInStore in store.Certificates)
            {
                if (certInStore.Thumbprint == "AFDC7074A87CCD7AE4236B88B465168D01C6675D")
                {
                    certificate = certInStore;
                    break;
                }
            }*/

            //ar cert = GetServerCertificate("localhost");

            _socket = CreateSocketBindAndListen(AddressFamily.InterNetwork, _ipAddress, _port);

            ServeSocket(_socket);
        }

        private void ServeSocket(Socket socket)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                while (!_shutdownInProgress)
                {
                    try
                    {
                        Socket acceptedSocket = socket.Accept();

                        new Thread(new ThreadStart(() =>
                        {
                            ServeConnection(acceptedSocket);
                        })).Start();
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
            });
        }

        private void ServeConnection(Socket acceptedSocket)
        {
            if (!_shutdownInProgress)
            {
                //Connection conn = new Connection(this, acceptedSocket, (System.Security.Cryptography.X509Certificates.X509Certificate)certificate);
                Connection conn = new Connection(this, acceptedSocket);

                if (conn.WaitForRequestBytes() == 0)
                {
                    conn.WriteErrorAndClose(400);
                    return;
                }

                ProcessRequest(conn);
            }
        }

        private void ProcessRequest(Connection conn)
        {
            var wrapper = new HttpConnectionWrapper(conn, this);

            string method, version;
            var url = wrapper.ReadInitialHeader(out method, out version);

            Console.WriteLine("{0} {1}", method, url);

            try
            {
                var host = _appHosts.GetHost(url);

                if (host == null)
                {
                    conn.WriteErrorAndClose(500);
                    return;
                }

                host.ProcessRequest(wrapper);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

                conn.WriteErrorAndClose(502);
            }
        }

        private System.Security.Cryptography.X509Certificates.X509Certificate GetServerCertificate(string name)
        {
            var kpgen = new RsaKeyPairGenerator();

            kpgen.Init(new KeyGenerationParameters(new SecureRandom(new CryptoApiRandomGenerator()), 2048));

            var cerKp = kpgen.GenerateKeyPair();

            /*var ca = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                File.ReadAllBytes(HttpContext.Current.Server.MapPath("~/App_Data/ca.pfx")), "test",
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);

            var parser = new X509CertificateParser();
            var caBC = parser.ReadCertificate(ca.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert));*/

            //var caPrivateKey = DotNetUtilities.GetKeyPair(ca.PrivateKey).Private;

            IList ord = new ArrayList();

            IDictionary attrs = new Hashtable();
            attrs[X509Name.CN] = name;
            attrs[X509Name.C] = "LT";

            ord.Add(X509Name.CN);
            ord.Add(X509Name.C);


            var certGen = new X509V3CertificateGenerator();

            certGen.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
            certGen.SetIssuerDN(new X509Name(ord, attrs));
            certGen.SetNotBefore(DateTime.Today.Subtract(new TimeSpan(1, 0, 0, 0)));
            certGen.SetNotAfter(DateTime.Today.AddDays(365));
            certGen.SetSubjectDN(new X509Name(ord, attrs));
            certGen.SetPublicKey(cerKp.Public);
            certGen.SetSignatureAlgorithm("SHA1WithRSA");
            certGen.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            certGen.AddExtension(X509Extensions.AuthorityKeyIdentifier, true, new AuthorityKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(cerKp.Public)));

            certGen.AddExtension(
                            X509Extensions.KeyUsage.Id,
                            false,
                            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment | KeyUsage.KeyAgreement));
            certGen.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                false,
                new ExtendedKeyUsage(KeyPurposeID.IdKPServerAuth, KeyPurposeID.IdKPClientAuth));


            Org.BouncyCastle.X509.X509Certificate x509 = certGen.Generate(cerKp.Private);

            System.Security.Cryptography.X509Certificates.X509Certificate x509_ = DotNetUtilities.ToX509Certificate(x509.CertificateStructure);
            System.Security.Cryptography.X509Certificates.X509Certificate2 cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(x509_);

            var prvKey = new RSACryptoServiceProvider();
            var prvParams = (RsaPrivateCrtKeyParameters)cerKp.Private;

            var rsaParameters = new RSAParameters();
            rsaParameters.Modulus = prvParams.Modulus.ToByteArrayUnsigned();
            rsaParameters.P = prvParams.P.ToByteArrayUnsigned();
            rsaParameters.Q = prvParams.Q.ToByteArrayUnsigned();
            rsaParameters.DP = prvParams.DP.ToByteArrayUnsigned();
            rsaParameters.DQ = prvParams.DQ.ToByteArrayUnsigned();
            rsaParameters.InverseQ = prvParams.QInv.ToByteArrayUnsigned();
            rsaParameters.D = prvParams.Exponent.ToByteArrayUnsigned();
            rsaParameters.Exponent = prvParams.PublicExponent.ToByteArrayUnsigned();

            prvKey.ImportParameters(rsaParameters);

            cert.PrivateKey = prvKey;

            return cert;
        }

        /// <summary>
        /// Allows an <see cref="T:System.Object"/> to attempt to free resources and perform other cleanup operations before the <see cref="T:System.Object"/> is reclaimed by garbage collection.
        /// </summary>
        ~Server()
        {
            Dispose();
        }


        private static Socket CreateSocketBindAndListen(AddressFamily family, IPAddress address, int port)
        {
            Socket socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(address, port));
            socket.Listen((int)SocketOptionName.MaxConnections);
            return socket;
        }

        //private void DecrementRequestCount()
        //{
        //    lock (_lockObject)
        //    {
        //        _requestCount--;

        //        if (_requestCount < 1)
        //        {
        //            _requestCount = 0;

        //            if (_timeoutInterval > 0 && _timer == null)
        //            {
        //                _timer = new Timer(TimeOut, null, _timeoutInterval, Timeout.Infinite);
        //            }
        //        }
        //    }
        //}
        
        //private void IncrementRequestCount()
        //{

        //    lock (_lockObject)
        //    {
        //        _requestCount++;

        //        if (_timer != null)
        //        {

        //            _timer.Dispose();
        //            _timer = null;
        //        }
        //    }
        //}


        private void ObtainProcessToken()
        {
            if (Interop.ImpersonateSelf(2))
            {
                Interop.OpenThreadToken(Interop.GetCurrentThread(), 0xf01ff, true, ref _processToken);
                Interop.RevertToSelf();
                // ReSharper disable PossibleNullReferenceException
                _processUser = WindowsIdentity.GetCurrent().Name;
                // ReSharper restore PossibleNullReferenceException
            }
        }

        private void OnRequestComplete(Guid id, LogInfo requestLog, LogInfo responseLog)
        {
            PublishLogToCommonLogging(requestLog);
            PublishLogToCommonLogging(responseLog);

            EventHandler<RequestEventArgs> complete = RequestComplete;


            if (complete != null)
            {
                complete(this, new RequestEventArgs(id, requestLog, responseLog));
            }
        }



        private void PublishLogToCommonLogging(LogInfo item)
        {
            if (!_useLogger)
            {
                return;
            }

            Common.Logging.ILog logger = Common.Logging.LogManager.GetCurrentClassLogger();

            var bodyAsString = String.Empty;
            try
            {
                bodyAsString = Encoding.UTF8.GetString(item.Body);
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
                /* empty bodies should be allowed */
            }

            var type = item.RowType == 0 ? "" : item.RowType == 1 ? "Request" : "Response";
            logger.Debug(type + " | " +
                          item.Created + " | " +
                          item.StatusCode + " | " +
                          item.Url + " | " +
                          item.PathTranslated + " | " +
                          item.Identity + " | " +
                          "\n===>Headers<====\n" + item.Headers +
                          "\n===>Body<=======\n" + bodyAsString
                );
        }


        ///<summary>
        ///</summary>
        public void ShutDown()
        {
            Console.WriteLine("Server shutdown");

            if (_shutdownInProgress)
            {
                return;
            }

            _shutdownInProgress = true;

            try
            {
                if (_socket != null)
                {
                    _socket.Close();
                }

                if (_managementSocket != null)
                {
                    _managementSocket.Close();
                }
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
                // TODO: why the swallow?
            }
            finally
            {
                _socket = null;
                _managementSocket = null;
            }

            try
            {
                if (_appHosts != null)
                {
                    _appHosts.Shutdown();
                }
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
                // TODO: what am i afraid of here?
            }

        }

        public void RegisterAdditionalMapping(string virtualDirectory, string physicalDirectory)
        {
            _appHosts.AddMapping(virtualDirectory, physicalDirectory);
        }

        //private void TimeOut(object ignored)
        //{
        //    TimeOut();
        //}

        /////<summary>
        /////</summary>
        //public void TimeOut()
        //{
        //    ShutDown();
        //    OnTimeOut();
        //}

        //private void OnTimeOut()
        //{
        //    EventHandler handler = TimedOut;
        //    if (handler != null) handler(this, EventArgs.Empty);
        //}

        /*internal void SignalDomainReload(string virtualPath)
        {
        }*/
    }
}