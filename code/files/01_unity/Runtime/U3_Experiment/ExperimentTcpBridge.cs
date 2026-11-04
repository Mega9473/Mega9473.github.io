using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Portfolio.Research
{
    /// <summary>
    /// dSPACEとのTCP通信だけを担当するクラス。
    /// 受信: displacement,button\n
    /// 送信: stiffnessId,vibrationId\n（0,0は停止）
    /// </summary>
    public sealed class ExperimentTcpBridge : MonoBehaviour
    {
        [Header("TCP Server")]
        [Tooltip("0.0.0.0なら、このPCのすべてのネットワークインターフェースで待ち受けます。")]
        [SerializeField] private string bindAddress = "0.0.0.0";
        [SerializeField, Min(1)] private int port = 12345;
        [SerializeField, Min(0.005f)] private float sendIntervalSeconds = 0.02f;

        [Header("dSPACE Button")]
        [SerializeField] private float buttonThreshold = 0.5f;
        [SerializeField, Min(0f)] private float debounceSeconds = 0.15f;

        [Header("Runtime Status (Read Only)")]
        [SerializeField] private float displacement;
        [SerializeField] private float buttonLevel;
        [SerializeField] private bool isListening;
        [SerializeField] private bool isClientConnected;
        [SerializeField] private int sentStiffnessId;
        [SerializeField] private int sentVibrationId;

        private readonly ConcurrentQueue<string> receivedLines = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> logMessages = new ConcurrentQueue<string>();
        private readonly object outputLock = new object();
        private readonly object socketLock = new object();

        private Thread serverThread;
        private TcpListener listener;
        private TcpClient activeClient;
        private volatile bool stopRequested;
        private bool lastButtonPressed;
        private float lastButtonEventTime = float.NegativeInfinity;

        public event Action NextRequested;

        public float Displacement => displacement;
        public float ButtonLevel => buttonLevel;
        public bool IsListening => isListening;
        public bool IsClientConnected => isClientConnected;
        public int SentStiffnessId => sentStiffnessId;
        public int SentVibrationId => sentVibrationId;

        private void OnEnable()
        {
            StartServer();
        }

        private void Update()
        {
            while (receivedLines.TryDequeue(out string line))
            {
                ProcessReceivedLine(line);
            }

            while (logMessages.TryDequeue(out string message))
            {
                Debug.Log(message, this);
            }
        }

        public void PublishCondition(int stiffnessId, int vibrationId)
        {
            lock (outputLock)
            {
                sentStiffnessId = stiffnessId;
                sentVibrationId = vibrationId;
            }
        }

        public void StartServer()
        {
            if (serverThread != null && serverThread.IsAlive)
            {
                return;
            }

            stopRequested = false;
            serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "SI2026 Experiment TCP Server"
            };
            serverThread.Start();
        }

        public void StopServer()
        {
            stopRequested = true;

            lock (socketLock)
            {
                try { activeClient?.Close(); } catch { }
                try { listener?.Stop(); } catch { }
            }

            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join(500);
            }

            serverThread = null;
            isListening = false;
            isClientConnected = false;
        }

        private void ServerLoop()
        {
            try
            {
                IPAddress address = ResolveBindAddress(bindAddress);
                TcpListener localListener = new TcpListener(address, port);

                lock (socketLock)
                {
                    listener = localListener;
                }

                localListener.Start();
                isListening = true;
                logMessages.Enqueue($"[ExperimentTcpBridge] Listening on {address}:{port}");

                while (!stopRequested)
                {
                    if (!localListener.Pending())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    TcpClient client = localListener.AcceptTcpClient();
                    lock (socketLock)
                    {
                        activeClient = client;
                    }

                    RunClientSession(client);

                    lock (socketLock)
                    {
                        if (ReferenceEquals(activeClient, client))
                        {
                            activeClient = null;
                        }
                    }
                }
            }
            catch (SocketException exception)
            {
                if (!stopRequested)
                {
                    logMessages.Enqueue($"[ExperimentTcpBridge] Socket error: {exception.Message}");
                }
            }
            catch (Exception exception)
            {
                if (!stopRequested)
                {
                    logMessages.Enqueue($"[ExperimentTcpBridge] Error: {exception}");
                }
            }
            finally
            {
                isListening = false;
                isClientConnected = false;
                lock (socketLock)
                {
                    try { activeClient?.Close(); } catch { }
                    try { listener?.Stop(); } catch { }
                    activeClient = null;
                    listener = null;
                }
            }
        }

        private void RunClientSession(TcpClient client)
        {
            try
            {
                client.NoDelay = true;
                isClientConnected = true;
                logMessages.Enqueue($"[ExperimentTcpBridge] Connected: {client.Client.RemoteEndPoint}");

                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] receiveBuffer = new byte[2048];
                    StringBuilder lineBuffer = new StringBuilder();
                    int sleepMilliseconds = Math.Max(
                        1,
                        (int)Math.Round(
                            sendIntervalSeconds * 1000f,
                            MidpointRounding.AwayFromZero));

                    while (!stopRequested && client.Connected)
                    {
                        if (stream.DataAvailable)
                        {
                            int length = stream.Read(receiveBuffer, 0, receiveBuffer.Length);
                            if (length <= 0)
                            {
                                break;
                            }

                            AppendReceivedText(lineBuffer, Encoding.UTF8.GetString(receiveBuffer, 0, length));
                        }

                        int stiffnessId;
                        int vibrationId;
                        lock (outputLock)
                        {
                            stiffnessId = sentStiffnessId;
                            vibrationId = sentVibrationId;
                        }

                        string message = stiffnessId.ToString(CultureInfo.InvariantCulture) + "," +
                            vibrationId.ToString(CultureInfo.InvariantCulture) + "\n";
                        byte[] sendBytes = Encoding.UTF8.GetBytes(message);
                        stream.Write(sendBytes, 0, sendBytes.Length);

                        Thread.Sleep(sleepMilliseconds);
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is SocketException ||
                exception is ObjectDisposedException)
            {
                if (!stopRequested)
                {
                    logMessages.Enqueue($"[ExperimentTcpBridge] Client disconnected: {exception.Message}");
                }
            }
            finally
            {
                isClientConnected = false;
            }
        }

        private void AppendReceivedText(StringBuilder lineBuffer, string text)
        {
            lineBuffer.Append(text);

            while (true)
            {
                int newlineIndex = FindNewline(lineBuffer);
                if (newlineIndex < 0)
                {
                    break;
                }

                string line = lineBuffer.ToString(0, newlineIndex).TrimEnd('\r');
                lineBuffer.Remove(0, newlineIndex + 1);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    receivedLines.Enqueue(line);
                }
            }
        }

        private static int FindNewline(StringBuilder text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private void ProcessReceivedLine(string line)
        {
            string[] values = line.Split(',');
            if (values.Length < 2 ||
                !TryParseFloat(values[0], out float receivedDisplacement) ||
                !TryParseFloat(values[1], out float receivedButton))
            {
                Debug.LogWarning($"[ExperimentTcpBridge] Invalid receive line: {line}", this);
                return;
            }

            displacement = receivedDisplacement;
            buttonLevel = receivedButton;

            bool isPressed = receivedButton >= buttonThreshold;
            if (isPressed && !lastButtonPressed &&
                Time.unscaledTime - lastButtonEventTime >= debounceSeconds)
            {
                lastButtonEventTime = Time.unscaledTime;
                NextRequested?.Invoke();
            }

            lastButtonPressed = isPressed;
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(
                text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static IPAddress ResolveBindAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || address == "0.0.0.0" || address == "*")
            {
                return IPAddress.Any;
            }

            if (!IPAddress.TryParse(address, out IPAddress parsedAddress))
            {
                throw new FormatException($"Invalid bind address: {address}");
            }

            return parsedAddress;
        }

        private void OnDisable()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void OnValidate()
        {
            port = Mathf.Clamp(port, 1, 65535);
            sendIntervalSeconds = Mathf.Max(0.005f, sendIntervalSeconds);
            debounceSeconds = Mathf.Max(0f, debounceSeconds);
        }
    }
}
