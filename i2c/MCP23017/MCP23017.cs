using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

public class MCP23017
{
    //MCP23017(Slave) 7 bit Address - Binary 0100 A2 A1 A0   (where A2[chip pin 17], A1[chip pin 16], A0[chip pin 15] are whether those three pins)
    //refer to figure 3-4 on page 15 of the datasheet
    private const byte MCP23017_ADDRESS = 0x20; //ensure all 3 address pins are pulled to ground

    //Port A (GPIOA*) Registers (reference table 3-3 on page 16 of the datasheet)
    private const byte IODIRA = 0x00; //Gpio direction of port A - input/output, defaults to input for all pins (all pins set to 1)
                                      //private const byte IPOLA = 0x02; //input polarity of port A, defaults to not inverted (all pins set to 0)
                                      //private const byte GPINTENA = 0x04; //interrupt on change pin of port A, default non-interrupting on change (all pins set to 0)
                                      //private const byte GPPUA = 0x0C; //register to set port A to pull up, default not pull up (all pins set to 0)
    private const byte GPIOA = 0x12; //register to read and write to GPIO pins on Port A, default all pins set to low (all pins set to 0)
    private const byte OLATA = 0x14; //output latch to set pins on port A to output low or high, default all pins set to low (all pins set to 0)

    //Port B (GPIOB*) Registers (reference table 3-3 on page 16 of the datasheet) (same defaults as counterpart on port A)
    private const byte IODIRB = 0x01; //Gpi direction of port B - input/output
                                      //private const byte IPOLB = 0x03; //input polarity of port B
                                      //private const byte GPINTENB = 0x05; //interrupt on change pin of port B
                                      //private const byte GPPUB = 0x0D; //register to set port B to pull up
    private const byte GPIOB = 0x13; //register to read and write to GPIO pins on Port B
    private const byte OLATB = 0x15; //output latch to set pins on port B to output low or high

    //Pin addresses dictionary (reference table 3-3 on page 16 of the data sheet
    private Dictionary<string, byte> _pins = new Dictionary<string, byte>() {
            { "GPIO0", 0x01 }, //0000 0001 (bit 7 to bit 0)       
            { "GPIO1", 0x02 }, //0000 0010  
            { "GPIO2", 0x04 }, //0000 0100
            { "GPIO3", 0x08 }, //0000 1000
            { "GPIO4", 0x10 }, //0001 0000
            { "GPIO5", 0x20 }, //0010 0000
            { "GPIO6", 0x40 }, //0100 0000
            { "GPIO7", 0x80 }  //1000 0000
        };


    //raspberry pi 3 b - only one I2C controller
    private const string I2C_CONTROLLER_NAME = "I2C1";

    // identifier whether we are working with the GPIO pins on port A or port B
    public enum GpioPort
    {
        A,
        B
    }

    //pins representing gpio pins on the expander regardless of port
    public enum GpioPin
    {
        GPIO0,
        GPIO1,
        GPIO2,
        GPIO3,
        GPIO4,
        GPIO5,
        GPIO6,
        GPIO7
    }


    private I2cDevice _mcp23017 = null;

    public async Task Initialize()
    {
        Debug.WriteLine("MCP23017: Initialize");

        try
        {
            var settings = new I2cConnectionSettings(MCP23017_ADDRESS);
            settings.BusSpeed = I2cBusSpeed.StandardMode;
            var deviceQuerySelector = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);
            var deviceInfoCollection = await DeviceInformation.FindAllAsync(deviceQuerySelector);

            _mcp23017 = await I2cDevice.FromIdAsync(deviceInfoCollection[0].Id, settings);

            if (_mcp23017 == null)
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

    /// <summary>
    /// This method preserves the OLAT / GPIO settings of all pins and
    /// will only modify the pin requested and set it to the directionality specified
    /// </summary>
    /// <param name="port">Port A or Port B</param>
    /// <param name="pin">GPIO 0-7</param>
    /// <param name="mode">Input or Output</param>
    public void SetGpioPinDriveMode(GpioPort port, GpioPin pin, GpioPinDriveMode mode)
    {
        byte olatRegister;
        byte iodirRegister;
        byte olatRegisterValues;
        byte iodirRegisterValues;

        if (port == GpioPort.A)
        {
            olatRegister = OLATA;
            iodirRegister = IODIRA;
        }
        else
        {
            olatRegister = OLATB;
            iodirRegister = IODIRB;
        }

        //get read values to preserve
        olatRegisterValues = _readRegister(olatRegister);
        iodirRegisterValues = _readRegister(iodirRegister);

        var pinAddress = _pins[pin.ToString()];

        switch (mode)
        {
            case GpioPinDriveMode.Input:
                var inputPinMask = (byte)(0x00 ^ pinAddress); //exclusive OR so that we only change the one pin - others remain unchanged
                iodirRegisterValues |= inputPinMask;
                break;
            case GpioPinDriveMode.Output:
                //output logic needs to be low prior to switching to output
                var outputPinMask = (byte)(0xFF ^ pinAddress);
                olatRegisterValues &= outputPinMask;
                _writeRegister(olatRegister, olatRegisterValues);
                iodirRegisterValues &= outputPinMask;
                break;
            default:
                Debug.WriteLine("Pin mode not current supported");
                throw new Exception("Pin mode not currently supported");
                break;
        }
        _writeRegister(iodirRegister, iodirRegisterValues);
    }


    public void Write(GpioPort port, GpioPin pin, GpioPinValue value)
    {
        byte olatRegister;
        byte olatRegisterValues;

        if (port == GpioPort.A)
        {
            olatRegister = OLATA;
        }
        else
        {
            olatRegister = OLATB;
        }

        olatRegisterValues = _readRegister(olatRegister);

        var pinAddress = _pins[pin.ToString()];
        if (value == GpioPinValue.Low)
        {
            var pinMask = (byte)(0xFF ^ pinAddress);
            olatRegisterValues &= pinMask;
        }
        else
        {
            //high
            olatRegisterValues |= pinAddress;
        }
        _writeRegister(olatRegister, olatRegisterValues);
    }

    public GpioPinValue Read(GpioPort port, GpioPin pin)
    {
        byte gpioRegister;
        byte gpioRegisterValues;

        if (port == GpioPort.A)
        {
            gpioRegister = GPIOA;
        }
        else
        {
            gpioRegister = GPIOB;
        }

        gpioRegisterValues = _readRegister(gpioRegister);
        var pinAddress = _pins[pin.ToString()];
        if ((byte)(gpioRegisterValues & pinAddress) == 0x00)
        {
            return GpioPinValue.Low;
        }
        else
        {
            return GpioPinValue.High;
        }
    }

    private byte _readRegister(byte registerAddress)
    {
        var readBuffer = new byte[1];
        _mcp23017.WriteRead(new byte[] { registerAddress }, readBuffer);
        return readBuffer[0];
    }

    private void _writeRegister(byte registerAddress, byte registerValue)
    {
        var writeBuffer = new byte[] { registerAddress, registerValue };
        _mcp23017.Write(writeBuffer);
    }
}