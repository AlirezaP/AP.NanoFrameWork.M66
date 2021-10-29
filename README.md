# AP.NanoFrameWork.M66 (Preview)

small helper for m66 r2:

'

           // COM2 in ESP32-WROVER-KIT mapped to free GPIO pins
           // mind to NOT USE pins shared with other devices, like serial flash and PSRAM
           // also it's MANDATORY to set pin function to the appropriate COM before instantiating it
           // set GPIO functions for COM2 (this is UART1 on ESP32)

            Configuration.SetPinFunction(17, DeviceFunction.COM2_TX);
            Configuration.SetPinFunction(16, DeviceFunction.COM2_RX);

            _gsmSerialDevice = SerialDevice.FromId("COM2");
            _gsmSerialDevice.BaudRate = 9600;// 9600;
            _gsmSerialDevice.Parity = SerialParity.None;
            _gsmSerialDevice.StopBits = SerialStopBitCount.One;
            _gsmSerialDevice.Handshake = SerialHandshake.None;
            _gsmSerialDevice.DataBits = 8;

            _apSerialHelper = new AP.NanoFrameWork.Serial.SerialHelper(_gsmSerialDevice);
            //_apSerialHelper.DataRecivedEventHandler += _apSerialHelper_DataRecivedEventHandler;

            _m66R2 = new AP.NanoFrameWork.M66.R2(_apSerialHelper, _pinNumberGSMPowerKey);
            _m66R2.SmsRecivedEventHandler += _m66R2_SmsRecivedEventHandler;
            
            
             _m66R2?.SendSms(message, "+98000000000");
                  
                  
        private static void _m66R2_SmsRecivedEventHandler(object source, AP.NanoFrameWork.M66.R2.SmsReciveEventArgs e)
        {
            Debug.WriteLine(e.Message);
        }
'
