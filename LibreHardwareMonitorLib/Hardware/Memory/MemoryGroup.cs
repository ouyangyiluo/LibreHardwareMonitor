// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;

namespace LibreHardwareMonitor.Hardware.Memory;
using System.Diagnostics;
using HidSharp;
using System.Xml.Linq;
using static LibreHardwareMonitor.Hardware.Memory.Spd5Hub;
using System.Security.Cryptography;

internal class MemoryGroup : IGroup
{
    private readonly List<Hardware> _hardware = new();

    public MemoryGroup(ushort smbusAddress, SMBios smbios, ISettings settings)
    {
        //_hardware = new Hardware[] {
        //    Software.OperatingSystem.IsUnix ? new GenericLinuxMemory("Generic Memory", settings) : new GenericWindowsMemory("Generic Memory", settings),

        //}; 
        AddHardware(smbusAddress,smbios, settings);
    }

    private void AddHardware(ushort smbusAddress, SMBios smbios, ISettings settings)
    {
        if (Software.OperatingSystem.IsUnix) {
            _hardware.Add(new GenericLinuxMemory("Generic Memory", settings));
        }
        else
        {
            int count = 0;
            int index = 0;
            foreach (var device in smbios.MemoryDevices)
            {
                if (device.Type == MemoryType.DDR5)
                {
                    if (!string.IsNullOrEmpty(device.PartNumber) && !string.IsNullOrEmpty(device.BankLocator) && !string.IsNullOrEmpty(device.DeviceLocator))
                    {
                        string name = $"{device.PartNumber} (#{index} {device.BankLocator} {device.DeviceLocator})";
                        Debug.WriteLine(device.PartNumber);

                        bool isSPD5118 = false;
                        if (smbusAddress != 0)
                        {
                            if(index >=0 && index < 8)
                            {
                                byte hid = (byte)(index & 0x07);
                                Spd5Hub spd5Hub = DetectSpd5Hub(smbusAddress, hid);
                                if (spd5Hub != null)
                                {
                                    isSPD5118 = true;
                                    _hardware.Add(new DDR5MemoryWithSpd5118(spd5Hub, device, name, settings));
                                }
                            }
                        }
                        if (!isSPD5118)
                        {
                            _hardware.Add(new Memory(index, device, name, settings));
                        }
                        count++;
                    }
                }
                index++;
            }
            if (count > 0)
            {
                return;
            }

            _hardware.Add(new GenericWindowsMemory("Generic Memory", settings));
        }

    }

    public string GetReport()
    {
        return null;
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public void Close()
    {
        foreach (Hardware ram in _hardware)
            ram.Close();
    }
}
