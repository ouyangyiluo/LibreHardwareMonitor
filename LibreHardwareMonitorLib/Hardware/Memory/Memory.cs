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

namespace LibreHardwareMonitor.Hardware.Memory;

internal class Memory : Hardware
{
    private readonly int _index;

    private readonly Sensor _memoryConfiguredSpeed;
    private readonly Sensor _memorySize;

    private readonly List<DimmSensor> _dimmSensorList = new List<DimmSensor>();

    public Memory(int index, MemoryDevice memoryDevice, string name, ISettings settings) : base(name, new Identifier($"ram#{index}"), settings)
    {
        _index = index;
        _memoryConfiguredSpeed = new Sensor("Configured Speed", 0, SensorType.Clock, this, settings);
        _memoryConfiguredSpeed.Value = memoryDevice.ConfiguredSpeed;
        ActivateSensor(_memoryConfiguredSpeed);

        _memorySize = new Sensor("Size", 0, SensorType.Data, this, settings);
        _memorySize.Value = memoryDevice.Size / 1024;
        ActivateSensor(_memorySize);
    }

    public override HardwareType HardwareType
    {
        get { return HardwareType.Memory; }
    }

    public override void Update()
    {

    }
}
