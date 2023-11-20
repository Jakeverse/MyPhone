using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace GoodTimeStudio.MyPhone.Pages
{
    public partial class CallPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? phoneNumber;

        [ObservableProperty]
        private int selectionLength;

        [ObservableProperty]
        private int selectionStart;

        /// <summary>
        /// Raise when user input phone number with button (no including keyboard)
        /// </summary>
        public event EventHandler<EventArgs>? PhoneNumberInputFocus;

        [RelayCommand]
        public void PressDigit(string digit)
        {
            PhoneNumberInputFocus?.Invoke(this, new EventArgs());
            if (SelectionLength != 0)
            {
                // We need to store it because when assigning a new value to PhoneNumber, the UI control will reset this 
                // Note: it will not be reset on unit test environment
                int start = SelectionStart;
                PhoneNumber = PhoneNumber!.Remove(SelectionStart, SelectionLength); // SelectionLength != 0 will gurantee PhoneNumber not null
                SelectionLength = 0;
                SelectionStart = start;
            }

            if (PhoneNumber != null && SelectionStart != PhoneNumber.Length)
            {
                PhoneNumber = PhoneNumber.Insert(SelectionStart, digit);
            }
            else
            {
                PhoneNumber += digit;
            }

            SelectionStart += 1;
        }

        [RelayCommand]
        public void PressBackSpace()
        {
            PhoneNumberInputFocus?.Invoke(this, new EventArgs());
            if (PhoneNumber != null)
            {
                int pos = SelectionStart;
                if (SelectionLength != 0)
                {
                    // There is a bug in SelectionBindingTextBox. 
                    // This dummy operation keep the SelectionStart in the right position after PhoneNumber.Remove()
                    SelectionStart = SelectionStart + SelectionLength; // DO NOT REMOVE THIS LINE

                    PhoneNumber = PhoneNumber!.Remove(pos, SelectionLength); // SelectionLength != 0 will gurantee PhoneNumber not null
                    SelectionLength = 0;
                    SelectionStart = pos;
                }
                else
                {
                    if (pos > 0)
                    {
                        PhoneNumber = PhoneNumber.Remove(pos - 1, 1);
                        SelectionStart = pos - 1;
                    }
                }
            }
        }

        [RelayCommand]
        public async Task Call()
        {
            /*Debug.Assert(App.Current.DeviceManager != null);
            var deviceManager = App.Current.DeviceManager;

            if (PhoneNumber != null)
            {
                if (deviceManager.CallService != null)
                {
                    await deviceManager.CallService.CallAsync(PhoneNumber);
                }
                else
                {
                    // TODO: what if CallService is not available 
                }
            }*/
            if (PhoneNumber != null)
            {
                Debug.Assert(App.Current.DeviceManager != null);
                var deviceManager = App.Current.DeviceManager;


                var add = deviceManager.CurrentDevice.HostName.ToString().Replace(":", "").Replace("(","").Replace(")","");
                Debug.WriteLine("address is " + add);
                Debug.WriteLine("address is " + deviceManager.CurrentDevice.HostName);
                
                BluetoothAddress address = BluetoothAddress.Parse(add); //"14876A84A213"
                BluetoothClient bluetoothClient = new BluetoothClient();
                try
                {
                    await Task.Run(delegate
                    {
                        bluetoothClient.Connect(address, BluetoothService.Handsfree);
                    });
                    using NetworkStream stream = bluetoothClient.GetStream();
                    List<string> cmds = new List<string>
                        {
                            "AT+CMER\r",
                            "AT+CIND=?\r",
                            "AT+BRSF=\r",
                            "ATD" + PhoneNumber + ";\r"
                        };
                    foreach (string cmd in cmds)
                    {
                        Debug.WriteLine("sending: " + cmd);
                        byte[] cmdData = Encoding.ASCII.GetBytes(cmd);
                        await stream.WriteAsync(cmdData, default(CancellationToken));
                        await stream.FlushAsync();
                        byte[] buffer = new byte[1024];
                        byte[] responseData = buffer.Take(await stream.ReadAsync(buffer, 0, buffer.Length)).ToArray();
                        string responseText = Encoding.ASCII.GetString(responseData).Trim();
                        Debug.WriteLine("responseText: " + responseText);
                    }
                }
                finally
                {
                    if (bluetoothClient != null)
                    {
                        ((IDisposable)bluetoothClient).Dispose();
                    }
                }
            }
        }

    }
}
