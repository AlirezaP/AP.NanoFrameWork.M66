using System;
using System.Diagnostics;
using System.Threading;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace AP.NanoFrameWork.Serial
{
    public class SerialHelper
    {
        private SerialDevice _serialDevice;
        private DataWriter _outputDataWriter;

        private AutoResetEvent serialEvent = new AutoResetEvent(false);
        private Timer _timer1;

        private string _expectedResponse = "";
        private int _responseTimeOut = 5000;
        private bool hasTimeOut = false;
        private bool _waiteForResponse = true;

        public delegate void MyEventHandler(object source, MyEventArgs e);
        public event MyEventHandler DataRecivedEventHandler;
        public SerialDataReceivedEventHandler RawDataReceivedEventHandler;


        public char WatchChar { get; set; } = '\r';

        public SerialHelper(SerialDevice serial, int responseTimeOut = 2000)
        {

            _serialDevice = serial;

            _responseTimeOut = responseTimeOut;
            var autoEvent = new AutoResetEvent(false);
      
            _timer1 = new Timer(CheckStatus, autoEvent, Timeout.Infinite, Timeout.Infinite);


            StartListener();
        }

        public void WriteToSerial(string data, bool waiteForResponse = true, bool enableTimeOut = false)
        {
            WriteToSerial(data, null, waiteForResponse, enableTimeOut);
        }

        private bool isTimeOut = false;
        public void WriteToSerial(string data, string expectedResponse, bool waiteForResponse = true, bool enableTimeOut = false)
        {

            Write(data, expectedResponse, waiteForResponse, enableTimeOut);

        }

        private void Write(string data, string expectedResponse, bool waiteForResponse, bool enableTimeOut)
        {
            //isTimeOut = false;

            //if (waiteForResponse)
            //{
            //    Debug.WriteLine("serialEvent.WaitOne");
            //    serialEvent.WaitOne();
            //}

            if (_outputDataWriter == null)
            {
                _outputDataWriter = new DataWriter(_serialDevice.OutputStream);
            }

            _outputDataWriter.WriteString(data);

            Thread.Sleep(100);

            _waiteForResponse = waiteForResponse;
            _expectedResponse = expectedResponse;

            Debug.WriteLine("Cmd Sent: " + data);

            if (!string.IsNullOrEmpty(expectedResponse) || enableTimeOut)
            {
                Debug.WriteLine("Serial Timer For Timeout detection was start " + _responseTimeOut.ToString());

                hasTimeOut = true;
                _timer1.Change(5000, _responseTimeOut);
                //  Thread.Sleep(8000);
            }



            if (waiteForResponse)
            {
                Debug.WriteLine("serialEvent.WaitOne");
                serialEvent.WaitOne();
            }


        }

        private void StartListener()
        {
            // setup read timeout
            // because we are reading from the UART it's recommended to set a read timeout
            // otherwise the reading operation doesn't return until the requested number of bytes has been read
            _serialDevice.ReadTimeout = new TimeSpan(0, 0, 4);

            // setup data read for Serial Device input stream
            DataReader inputDataReader = new DataReader(_serialDevice.InputStream);

            // setup an event handler that will fire when a char is received in the serial device input stream
            _serialDevice.DataReceived += _serialDevice_DataReceived;

            // set a watch char to be notified when it's available in the input stream
            _serialDevice.WatchChar = WatchChar;

            Debug.WriteLine("start DataReceived");
        }

        private void _serialDevice_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //....................................

            RawDataReceivedEventHandler?.Invoke(sender, e);

            //.....................................


            if (e.EventType == SerialData.Chars)
            {
                 Debug.WriteLine("rx chars");
            }
            else if (e.EventType == SerialData.WatchChar)
            {
                if (isTimeOut)
                {
                    Debug.WriteLine("isTimeOut = true so return from _serialDevice_DataReceived");
                    return;
                }

                Thread.Sleep(100);

                SerialDevice serialDevice = (SerialDevice)sender;

                using (DataReader inputDataReader = new DataReader(serialDevice.InputStream))
                {
                    if (hasTimeOut && (string.IsNullOrEmpty(_expectedResponse)))
                    {
                        _timer1.Change(Timeout.Infinite, Timeout.Infinite);
                        hasTimeOut = false;
                    }

                    inputDataReader.InputStreamOptions = InputStreamOptions.Partial;

                    // read all available bytes from the Serial Device input stream
                    var bytesRead = inputDataReader.Load(serialDevice.BytesToRead);

                    if (bytesRead > 0)
                    {
                        String temp = inputDataReader.ReadString(bytesRead);

                        Debug.WriteLine("String: >>" + temp + "<< ");

                        if (string.IsNullOrEmpty(_expectedResponse) && _waiteForResponse)
                        {
                            Debug.WriteLine("serialEvent.Set");
                            serialEvent.Set();
                            serialEvent.Reset();
                        }

                        Thread.Sleep(100);



                        if (!string.IsNullOrEmpty(_expectedResponse) && temp.ToLower().Contains(_expectedResponse.ToLower()))
                        {
                            Debug.WriteLine("_expectedResponse found ... serialEvent.Set");

                            if (_waiteForResponse)
                            {
                                serialEvent.Set();
                                serialEvent.Reset();
                            }

                            if (hasTimeOut)
                            {
                                _timer1.Change(Timeout.Infinite, Timeout.Infinite);
                                hasTimeOut = false;
                            }
                        }

                        
                            DataRecivedEventHandler?.Invoke(this, new MyEventArgs(temp));
                        
                        
                    }
                }
            }
        }


        private void CheckStatus(Object stateInfo)
        {
            Debug.WriteLine("Time Out Trigger");

            isTimeOut = true;

            DataRecivedEventHandler?.Invoke(this, new MyEventArgs("TimeOut!"));

            _timer1.Change(Timeout.Infinite, Timeout.Infinite);

            serialEvent.Set();
            serialEvent.Reset();

            isTimeOut = false;
        }

        public class MyEventArgs : EventArgs
        {
            private string EventInfo;

            public MyEventArgs(string Text)
            {
                EventInfo = Text;
            }

            public string GetInfo()
            {
                return EventInfo;
            }
        }


        private class SerialWriterMetaData
        {
            public string data { get; set; }

            public string expectedResponse { get; set; }

            public bool waiteForResponse { get; set; }

            public bool enableTimeOut { get; set; }

        }
    }
}
