// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using LibreHardwareMonitor.Interop;
using LibreHardwareMonitor.Hardware.Cpu;
using System.Globalization;
using System.Text;
using LibreHardwareMonitor.Hardware.Motherboard;

namespace LibreHardwareMonitor.Hardware.Memory;

internal sealed class DDR5MemoryWithSpd5118 : Memory
{
    private readonly Spd5Hub _spd5Hub;

    private readonly Sensor _memoryConfiguredSpeed;
    private readonly Sensor _memorySize;
    private readonly Sensor _temperature;
    //private string _spd5HubModuleSerialNumber;
    private string _memoryDeviceModuleSerialNumber;

    public DDR5MemoryWithSpd5118(Spd5Hub spd5Hub, MemoryDevice memoryDevice, string name, ISettings settings) : base(spd5Hub.Hid, memoryDevice, name, settings)
    {
        _spd5Hub = spd5Hub;

        _temperature = new Sensor("SPD5 Hub Temperature", 0, SensorType.Temperature, this, settings);
        ActivateSensor(_temperature);



        //_spd5Hub.SetThermalSensorConfiguration(disableThermalSensor: false);

        //_spd5HubModuleSerialNumber = spd5Hub.GetModuleSerialNumber();
        _memoryDeviceModuleSerialNumber = memoryDevice.SerialNumber;


    }
    public override void Update()
    {
        float temperature = 0f;
        if (_spd5Hub.GetTemperature(ref temperature))
            _temperature.Value = temperature;
       
    }

    public override string GetReport()
    {
        StringBuilder r = new(base.GetReport());
        //r.AppendLine($"SPD5 Hub Module Serial Number: {_spd5HubModuleSerialNumber}");
        r.AppendLine($"Memory Device Module Serial Number: {_memoryDeviceModuleSerialNumber}");
        
        return r.ToString();
    }
}
