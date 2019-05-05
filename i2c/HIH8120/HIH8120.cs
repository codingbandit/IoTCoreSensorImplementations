using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

public class HIH8120
{
    const byte HIH8120_ADDRESS = 0x27;
    const string I2C_CONTROLLER_NAME = "I2C1";
    private I2cDevice _hih8120 = null;

    public async Task Initialize()
    {
        Debug.WriteLine("HIH8120: Initialize");
        try
        {
            I2cConnectionSettings settings = new I2cConnectionSettings(HIH8120_ADDRESS);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            var deviceQuerySelector = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);
            var deviceInfoCollection = await DeviceInformation.FindAllAsync(deviceQuerySelector);
            _hih8120 = await I2cDevice.FromIdAsync(deviceInfoCollection[0].Id, settings);
            if (_hih8120 == null)
            {
                Debug.WriteLine("Device Not Found");
            }
            else
            {
                Debug.WriteLine("Device Initialized");
            }

        }
        catch (Exception ex)
        {
            Debug.WriteLine("Exception: " + ex.Message);
            throw;
        }
    }

    public async Task<HIH8120_Reading> TakeReadingAsync()
    {
        var readBuffer = new byte[4];
        var measureRequestResult = _hih8120.WritePartial(new byte[] { 0 });
        await Task.Delay(1000);
        var readRequest = _hih8120.WritePartial(new byte[] { 1 });
        await Task.Delay(1000);
        var wrPartialResult = _hih8120.ReadPartial(readBuffer);

        var status = (readBuffer[0] >> 6);

        switch (status)
        {
            case 0:
                Debug.WriteLine("Normal Operation");
                break;
            case 1:
                Debug.WriteLine("Stale Data");
                break;
            case 2:
                Debug.WriteLine("Command mode");
                break;
            case 3:
                Debug.WriteLine("Diagnostic");
                break;
        }

        //byte 0    byte 1      byte 2      byte 3
        //sshhhhhh  hhhhhhhh    tttttttt    ttttttXX <-- s=status bits, h=humid bits, X=don't care
        //00111111  11111111 <-- humidity mask 0x3f

        var humidityReading = ((readBuffer[0] & 0x3f) << 8) | readBuffer[1];
        Debug.WriteLine("Humidity Bits: " + Convert.ToString(humidityReading, 2));
        var tempReading = (readBuffer[2] << 6) | (readBuffer[3] >> 2);
        Debug.WriteLine("Temperature Bits: " + Convert.ToString(tempReading, 2));
        HIH8120_Reading reading = new HIH8120_Reading();

        reading.RelativeHumidity = ((humidityReading * 100) / (Math.Pow(2, 14) - 1));
        reading.TemperatureC = (tempReading / (Math.Pow(2, 14) - 1)) * 165 - 40;
        reading.TemperatureF = reading.TemperatureC * 1.8 + 32;

        Debug.WriteLine("Relative Humidity: " + reading.RelativeHumidity);
        Debug.WriteLine("Temp C: " + reading.TemperatureC);
        Debug.WriteLine("Temp F: " + reading.TemperatureF);

        return reading;

    }


}

public class HIH8120_Reading
{
    public double RelativeHumidity { get; set; }
    public double TemperatureC { get; set; }
    public double TemperatureF { get; set; }
}

