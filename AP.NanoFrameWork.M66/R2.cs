
using System;
using System.Diagnostics;
using System.Threading;
using Windows.Devices.Gpio;

namespace AP.NanoFrameWork.M66
{
    public class R2
    {
        private AP.NanoFrameWork.Serial.SerialHelper _serialHelper;

        private Timer _timer1;
        private GpioPin _dtrPin = null;

        System.Collections.Queue sendQ = new System.Collections.Queue();

        private bool _isReady = false;
        public bool IsReady
        {
            get
            {
                return _isReady;
            }
        }

        public delegate void SmsReciveEventHandler(object source, SmsReciveEventArgs e);
        public event SmsReciveEventHandler SmsRecivedEventHandler;

        private int _m66DTRPinNumber;

        public R2(AP.NanoFrameWork.Serial.SerialHelper serialHelper, int m66DTRPinNumber)
        {
            _m66DTRPinNumber = m66DTRPinNumber;
            _serialHelper = serialHelper;
            Initial(_m66DTRPinNumber);


            var autoEvent = new AutoResetEvent(false);
            _timer1 = new Timer(CheckStatus, autoEvent, 2000, 1000);

            var autoEvent2 = new AutoResetEvent(false);
        }

        private void Initial(int m66DTRPinNumber)
        {

            var gpioController = new GpioController();

            _dtrPin = gpioController.OpenPin(m66DTRPinNumber);
            _dtrPin.SetDriveMode(GpioPinDriveMode.Output);

            ResetForCommandMode();



            _serialHelper.DataRecivedEventHandler += _serialHelper_DataRecivedEventHandler;

            //(new Thread(() =>
            //{
            //    _serialHelper.DataRecivedEventHandler += _serialHelper_DataRecivedEventHandler;

            //    //_serialHelper.WriteToSerial("AT\r",false,false);
            //    InitialSMSMode();

            //})).Start();


        }

        private void InitialSMSMode()
        {
            _serialHelper.WriteToSerial("AT+GSN=?\r\n", "ok", true, true);
            _serialHelper.WriteToSerial("AT+CMGD=1,4\r\n", "ok", true, true);
            _serialHelper.WriteToSerial("AT+IPR=9600\r\n", "ok");
            _serialHelper.WriteToSerial("AT+CMGF=1\r\n", "ok");
            _serialHelper.WriteToSerial("AT+CSCS=\"GSM\"\r\n", "ok");
            //_serialHelper.WriteToSerial("AT+CCLK?\r\n");


            //  _serialHelper.WriteToSerial("AT+CSMP=\"SM\",\"SM\",\"SM\"\n");

            _isReady = true;

            //AT+CCLK?
        }

        public void SendSms(string message, string toPhoneNumber)
        {
            sendQ.Enqueue(new SmsSendDataModel
            {
                data = $"AT+CMGS=\"{toPhoneNumber}\"\n{message}\x1a",
                enableTimeOut = false,
                waiteForResponse = true,
                expectedResponse = "ok"
            });

            Thread.Sleep(1000);

        }

        static int resetCounter = 0;
        private void _serialHelper_DataRecivedEventHandler(object source, Serial.SerialHelper.MyEventArgs e)
        {


            string dataRecived = e.GetInfo();

            Debug.WriteLine("from Event: " + dataRecived + " End event");

            Thread.Sleep(100);

            if (dataRecived.Contains("TimeOut!"))
            {
                resetCounter++;

                SmsRecivedEventHandler?.Invoke(this, new SmsReciveEventArgs(dataRecived, "", "", ""));

                resetCounter = 0;

                Debug.WriteLine("Suspect ToRest!");
                suspectToreset = false;
                ResetForCommandMode();
                Debug.WriteLine("Rest Done");
                Debug.WriteLine("Continue After Reset");


            }

            //If Recive Sms Read it
            //.....................................................

            if (dataRecived.Contains("POWER DOWN"))
            {
                ResetForCommandMode();
            }

            if (dataRecived.Contains("+CMGR:"))
            {
                int index1 = dataRecived.IndexOf("+CMGR:");
                string b = dataRecived.Substring(index1, (dataRecived.Length - index1));

                string[] c = b.Split(',');

                var status = c[0];
                var phoneNumber = c[1];

                var payload = c[c.Length - 1].Split('\r');
                var date = payload[0];
                var txt = payload[1];


                SmsRecivedEventHandler?.Invoke(this, new SmsReciveEventArgs(txt, phoneNumber, status, date));
                Thread.Sleep(100);
                //  _serialHelper.WriteToSerial("AT+CMGD=1,4\r", "ok");

                sendQ.Enqueue(new SmsSendDataModel
                {
                    data = "AT+CMGD=1,4\r",
                    enableTimeOut = false,
                    waiteForResponse = true,
                    expectedResponse = "ok"
                });
            }



            if (dataRecived.Contains("+CMTI:") && dataRecived.Contains("SM"))
            {
                var rawINcomingData = dataRecived.Split(('\r'));

                foreach (var item in rawINcomingData)
                {
                    Thread.Sleep(100);

                    if (item.Contains("+CMTI:") && item.Contains("SM"))
                    {
                        var myindex = item.Split(',')[1];
                        Debug.WriteLine(myindex);
                        Thread.Sleep(100);

                        sendQ.Enqueue(new SmsSendDataModel
                        {
                            data = "AT+CMGR=" + myindex + "\r",
                            enableTimeOut = false,
                            waiteForResponse = false,
                            expectedResponse = null
                        });


                        Thread.Sleep(100);
                    }
                }
            }


        }

        private void SendCommand(string cmd, string expectedResponse, bool waiteForResponse = false, bool enableTimeOut = false)
        {
            sendQ.Enqueue(new SmsSendDataModel
            {
                data = cmd,
                enableTimeOut = enableTimeOut,
                waiteForResponse = waiteForResponse,
                expectedResponse = expectedResponse
            });
        }
        private void ResetForCommandMode(bool reverse = false)
        {
            Debug.WriteLine("Reset M66");

            if (reverse)
            {
                _dtrPin.Write(GpioPinValue.High);
                Thread.Sleep(1000);
                _dtrPin.Write(GpioPinValue.Low);
                Thread.Sleep(600);
            }
            else
            {
                _dtrPin.Write(GpioPinValue.Low);
                Thread.Sleep(1000);
                _dtrPin.Write(GpioPinValue.High);
                Thread.Sleep(600);
            }

        }

        private void CheckStatus(Object stateInfo)
        {
            if (sendQ.Count <= 0)
            {
                return;
            }

            var q = sendQ.Dequeue();

            if (q == null)
            {
                return;
            }

            var rr = ((SmsSendDataModel)q);

            Thread t = new Thread(() =>
              {
                  _serialHelper.WriteToSerial
                      (rr.data, rr.expectedResponse, rr.waiteForResponse, rr.enableTimeOut);
              });
            t.Priority = ThreadPriority.BelowNormal;

            t.Start();
        }

        private bool suspectToreset = false;
   

        public class SmsReciveEventArgs : EventArgs
        {
            private string status;
            private string messageDateTime;
            private string message;
            private string phoneNumber;

            public string Status
            {
                get
                {
                    return status;
                }
            }

            public string MessageDateTime
            {
                get
                {
                    return messageDateTime;
                }
            }


            public string Message
            {
                get
                {
                    return message;
                }
            }

            public string PhoneNumber
            {
                get
                {
                    return phoneNumber;
                }
            }

            public SmsReciveEventArgs(string Text, string phone, string status, string dateTime)
            {
                this.message = Text;
                this.phoneNumber = phone;
                this.status = status;
                this.messageDateTime = dateTime;
            }

        }

        public class SmsSendDataModel
        {
            public string data { get; set; }

            public string expectedResponse { get; set; }

            public bool waiteForResponse { get; set; }

            public bool enableTimeOut { get; set; }
        }
    }
}
