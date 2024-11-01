// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using LibreHardwareMonitor.Hardware.Battery;
using LibreHardwareMonitor.Hardware.Controller.AeroCool;
using LibreHardwareMonitor.Hardware.Controller.AquaComputer;
using LibreHardwareMonitor.Hardware.Controller.Heatmaster;
using LibreHardwareMonitor.Hardware.Controller.Nzxt;
using LibreHardwareMonitor.Hardware.Controller.Razer;
using LibreHardwareMonitor.Hardware.Controller.TBalancer;
using LibreHardwareMonitor.Hardware.Cpu;
using LibreHardwareMonitor.Hardware.Gpu;
using LibreHardwareMonitor.Hardware.Memory;
using LibreHardwareMonitor.Hardware.Motherboard;
using LibreHardwareMonitor.Hardware.Network;
using LibreHardwareMonitor.Hardware.Psu.Corsair;
using LibreHardwareMonitor.Hardware.Storage;

namespace LibreHardwareMonitor.Hardware;

/// <summary>
/// Stores all hardware groups and decides which devices should be enabled and updated.
/// </summary>
public class Computer : IComputer
{
    private readonly List<IGroup> _groups = new();
    private readonly object _lock = new();
    private readonly ISettings _settings;
        
    private bool _batteryEnabled;
    private bool _controllerEnabled;
    private bool _cpuEnabled;
    private bool _gpuEnabled;
    private bool _memoryEnabled;
    private bool _motherboardEnabled;
    private bool _networkEnabled;
    private bool _open;
    private bool _psuEnabled;
    private SMBios _smbios;
    private bool _storageEnabled;
    private ushort _smbusAddress;

    /// <summary>
    /// Creates a new <see cref="IComputer" /> instance with basic initial <see cref="Settings" />.
    /// </summary>
    public Computer()
    {
        _settings = new Settings();
    }

    /// <summary>
    /// Creates a new <see cref="IComputer" /> instance with additional <see cref="ISettings" />.
    /// </summary>
    /// <param name="settings">Computer settings that will be transferred to each <see cref="IHardware" />.</param>
    public Computer(ISettings settings)
    {
        _settings = settings ?? new Settings();
    }

    /// <inheritdoc />
    public event HardwareEventHandler HardwareAdded;

    /// <inheritdoc />
    public event HardwareEventHandler HardwareRemoved;

    /// <inheritdoc />
    public IList<IHardware> Hardware
    {
        get
        {
            lock (_lock)
            {
                List<IHardware> list = new();

                foreach (IGroup group in _groups)
                    list.AddRange(group.Hardware);

                return list;
            }
        }
    }

    /// <inheritdoc />
    public bool IsBatteryEnabled
    {
        get { return _batteryEnabled; }
        set
        {
            if (_open && value != _batteryEnabled)
            {
                if (value)
                {
                    Add(new BatteryGroup(_settings));
                }
                else
                {
                    RemoveType<BatteryGroup>();
                }
            }

            _batteryEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsControllerEnabled
    {
        get { return _controllerEnabled; }
        set
        {
            if (_open && value != _controllerEnabled)
            {
                if (value)
                {
                    Add(new TBalancerGroup(_settings));
                    Add(new HeatmasterGroup(_settings));
                    Add(new AquaComputerGroup(_settings));
                    Add(new AeroCoolGroup(_settings));
                    Add(new NzxtGroup(_settings));
                    Add(new RazerGroup(_settings));
                }
                else
                {
                    RemoveType<TBalancerGroup>();
                    RemoveType<HeatmasterGroup>();
                    RemoveType<AquaComputerGroup>();
                    RemoveType<AeroCoolGroup>();
                    RemoveType<NzxtGroup>();
                    RemoveType<RazerGroup>();
                }
            }

            _controllerEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsCpuEnabled
    {
        get { return _cpuEnabled; }
        set
        {
            if (_open && value != _cpuEnabled)
            {
                if (value)
                    Add(new CpuGroup(_settings));
                else
                    RemoveType<CpuGroup>();
            }

            _cpuEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsGpuEnabled
    {
        get { return _gpuEnabled; }
        set
        {
            if (_open && value != _gpuEnabled)
            {
                if (value)
                {
                    Add(new AmdGpuGroup(_settings));
                    Add(new NvidiaGroup(_settings));
                    Add(new IntelGpuGroup(GetIntelCpus(), _settings));
                }
                else
                {
                    RemoveType<AmdGpuGroup>();
                    RemoveType<NvidiaGroup>();
                    RemoveType<IntelGpuGroup>();
                }
            }

            _gpuEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsMemoryEnabled
    {
        get { return _memoryEnabled; }
        set
        {
            if (_open && value != _memoryEnabled)
            {
                if (value)
                    Add(new MemoryGroup(_smbusAddress, _smbios, _settings));
                else
                    RemoveType<MemoryGroup>();
            }

            _memoryEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsMotherboardEnabled
    {
        get { return _motherboardEnabled; }
        set
        {
            if (_open && value != _motherboardEnabled)
            {
                if (value)
                    Add(new MotherboardGroup(_smbios, _settings));
                else
                    RemoveType<MotherboardGroup>();
            }

            _motherboardEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsNetworkEnabled
    {
        get { return _networkEnabled; }
        set
        {
            if (_open && value != _networkEnabled)
            {
                if (value)
                    Add(new NetworkGroup(_settings));
                else
                    RemoveType<NetworkGroup>();
            }

            _networkEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsPsuEnabled
    {
        get { return _psuEnabled; }
        set
        {
            if (_open && value != _psuEnabled)
            {
                if (value)
                {
                    Add(new CorsairPsuGroup(_settings));
                }
                else
                {
                    RemoveType<CorsairPsuGroup>();
                }
            }

            _psuEnabled = value;
        }
    }

    /// <inheritdoc />
    public bool IsStorageEnabled
    {
        get { return _storageEnabled; }
        set
        {
            if (_open && value != _storageEnabled)
            {
                if (value)
                    Add(new StorageGroup(_settings));
                else
                    RemoveType<StorageGroup>();
            }

            _storageEnabled = value;
        }
    }

    /// <summary>
    /// Contains computer information table read in accordance with <see href="https://www.dmtf.org/standards/smbios">System Management BIOS (SMBIOS) Reference Specification</see>.
    /// </summary>
    public SMBios SMBios
    {
        get
        {
            if (!_open)
                throw new InvalidOperationException("SMBIOS cannot be accessed before opening.");

            return _smbios;
        }
    }

    //// <inheritdoc />
    public string GetReport()
    {
        lock (_lock)
        {
            using StringWriter w = new(CultureInfo.InvariantCulture);

            w.WriteLine();
            w.WriteLine(nameof(LibreHardwareMonitor) + " Report");
            w.WriteLine();

            Version version = typeof(Computer).Assembly.GetName().Version;

            NewSection(w);
            w.Write("Version: ");
            w.WriteLine(version.ToString());
            w.WriteLine();

            NewSection(w);
            w.Write("Common Language Runtime: ");
            w.WriteLine(Environment.Version.ToString());
            w.Write("Operating System: ");
            w.WriteLine(Environment.OSVersion.ToString());
            w.Write("Process Type: ");
            w.WriteLine(IntPtr.Size == 4 ? "32-Bit" : "64-Bit");
            w.WriteLine();

            string r = Ring0.GetReport();
            if (r != null)
            {
                NewSection(w);
                w.Write(r);
                w.WriteLine();
            }

            NewSection(w);
            w.WriteLine("Sensors");
            w.WriteLine();

            foreach (IGroup group in _groups)
            {
                foreach (IHardware hardware in group.Hardware)
                    ReportHardwareSensorTree(hardware, w, string.Empty);
            }

            w.WriteLine();

            NewSection(w);
            w.WriteLine("Parameters");
            w.WriteLine();

            foreach (IGroup group in _groups)
            {
                foreach (IHardware hardware in group.Hardware)
                    ReportHardwareParameterTree(hardware, w, string.Empty);
            }

            w.WriteLine();

            foreach (IGroup group in _groups)
            {
                string report = group.GetReport();
                if (!string.IsNullOrEmpty(report))
                {
                    NewSection(w);
                    w.Write(report);
                }

                foreach (IHardware hardware in group.Hardware)
                    ReportHardware(hardware, w);
            }

            return w.ToString();
        }
    }

    /// <summary>
    /// Triggers the <see cref="IVisitor.VisitComputer" /> method for the given observer.
    /// </summary>
    /// <param name="visitor">Observer who call to devices.</param>
    public void Accept(IVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        visitor.VisitComputer(this);
    }

    /// <summary>
    /// Triggers the <see cref="IElement.Accept" /> method with the given visitor for each device in each group.
    /// </summary>
    /// <param name="visitor">Observer who call to devices.</param>
    public void Traverse(IVisitor visitor)
    {
        lock (_lock)
        {
            // Use a for-loop instead of foreach to avoid a collection modified exception after sleep, even though everything is under a lock.
            for (int i = 0; i < _groups.Count; i++)
            {
                IGroup group = _groups[i];

                for (int j = 0; j < group.Hardware.Count; j++)
                    group.Hardware[j].Accept(visitor);
            }
        }
    }

    private void HardwareAddedEvent(IHardware hardware)
    {
        HardwareAdded?.Invoke(hardware);
    }

    private void HardwareRemovedEvent(IHardware hardware)
    {
        HardwareRemoved?.Invoke(hardware);
    }

    private void Add(IGroup group)
    {
        if (group == null)
            return;

        lock (_lock)
        {
            if (_groups.Contains(group))
                return;

            _groups.Add(group);

            if (group is IHardwareChanged hardwareChanged)
            {
                hardwareChanged.HardwareAdded += HardwareAddedEvent;
                hardwareChanged.HardwareRemoved += HardwareRemovedEvent;
            }
        }

        if (HardwareAdded != null)
        {
            foreach (IHardware hardware in group.Hardware)
                HardwareAdded(hardware);
        }
    }

    private void Remove(IGroup group)
    {
        lock (_lock)
        {
            if (!_groups.Contains(group))
                return;

            _groups.Remove(group);

            if (group is IHardwareChanged hardwareChanged)
            {
                hardwareChanged.HardwareAdded -= HardwareAddedEvent;
                hardwareChanged.HardwareRemoved -= HardwareRemovedEvent;
            }
        }

        if (HardwareRemoved != null)
        {
            foreach (IHardware hardware in group.Hardware)
                HardwareRemoved(hardware);
        }

        group.Close();
    }

    private void RemoveType<T>() where T : IGroup
    {
        List<T> list = new();

        lock (_lock)
        {
            foreach (IGroup group in _groups)
            {
                if (group is T t)
                    list.Add(t);
            }
        }

        foreach (T group in list)
            Remove(group);
    }

    //public enum BaseClassType : byte
    //{
    //    Obsolete = 0x00,
    //    Storage = 0x01,
    //    Network = 0x02,
    //    Display = 0x03,
    //    Multimedia = 0x04,
    //    Memory = 0x05,
    //    Bridge = 0x06,
    //    Communication = 0x07,
    //    System = 0x08,
    //    Input = 0x09,
    //    Docking = 0x0A,
    //    Processor = 0x0B,
    //    Serial = 0x0C,
    //    Wireless = 0x0D,
    //    Intelligent = 0x0E,
    //    Satellite = 0x0F,
    //    Encryption = 0x10,
    //    Processing = 0x11,
    //    Accelerator = 0x12,
    //    Instrumentation = 0x13,
    //    Undefined = 0xFF
    //}

    ///// <summary>
    ///// PCI Sub Class codes
    ///// </summary>
    //public enum SubClassType : byte
    //{
    //    Isa = 0x01,
    //    Smbus = 0x05,
    //}

    //private static byte _maxPciBusIndex = 255;
    //private static int MaxPciCount => (_maxPciBusIndex + 1) * 256 /*(MAX_PCI_DEVICE_INDEX + 1) * (MAX_PCI_FUNCTION_INDEX + 1)*/;

    //public static void FindDeviceByClass(BaseClassType baseClass, SubClassType subClass, byte programIf, int maxCount)
    //{

    //    //if (maxCount > MaxPciCount || maxCount == 0)
    //    //if (IsOutside(maxCount, 1, MaxPciCount))
    //    //{
    //    //    throw new ArgumentOutOfRangeException(nameof(maxCount));
    //    //}

    //    Queue<PciConfig> result = new Queue<PciConfig>();

    //    bool stopFlag = false;

    //    if (LockPciMutex())
    //    {

    //        PciConfig pciConfig = new PciConfig();

    //        // Bus loop
    //        for (short bus = 0; bus <= _maxPciBusIndex && !stopFlag; bus++)
    //        {
    //            // Device loop
    //            for (byte dev = 0; dev <= MaxPciDeviceIndex && !stopFlag; dev++)
    //            {

    //                pciConfig.SetBus((byte)bus).SetDevice(dev).SetFunction(0);

    //                DeviceId devId = pciConfig.DeviceId;

    //                if (devId == ushort.MinValue ||
    //                    devId == DeviceId.Invalid)
    //                {
    //                    continue;
    //                }

    //                for (byte func = 0; func <= MaxPciFunctionIndex; func++)
    //                {

    //                    pciConfig.SetFunction(func);

    //                    if ((Kernel.ReadPciConfig<uint>((byte)bus, dev, func, 0x08) & 0xFFFFFF00) !=
    //                        (uint)(((byte)baseClass << 24) |
    //                               ((byte)subClass << 16) |
    //                               ((byte)programIf << 8)))
    //                    {
    //                        continue;
    //                    }

    //                    result.Enqueue(new PciConfig(pciConfig));

    //                    if (result.Count != maxCount)
    //                    {
    //                        continue;
    //                    }

    //                    stopFlag = true;
    //                    break;
    //                }
    //            }
    //        }

    //        UnlockPciMutex();
    //    }

    //    return result.ToArray();
    //}


    public UInt32 MakePciConfigAddress(UInt32 functionNumber, UInt32 deviceNumber, UInt32 busNumber)
    {
        UInt32 pciConfigAddress = ((functionNumber & 7) | ((deviceNumber & 0x1F) << 3) | (busNumber & 0xFF) << 8);
        return pciConfigAddress;
    }

  
    const uint PCIConfigurationOffset_VendorID = 0x00;
    const uint PCIConfigurationOffset_DeviceID = 0x02;
    const uint PCIConfigurationOffset_RevisionID_and_ClassCode = 0x08;

    const ushort IOMappedControlRegisters_PM_Index = 0xCD6;
    const ushort IOMappedControlRegisters_PM_Data = 0xCD7;


    public byte PMRead(byte offsetAddress)
    {
        Ring0.WriteIoPort(IOMappedControlRegisters_PM_Index, (offsetAddress));
        return Ring0.ReadIoPort(IOMappedControlRegisters_PM_Data);
    }


    /// <summary>
    /// If hasn't been opened before, opens <see cref="SMBios" />, <see cref="Ring0" />, <see cref="OpCode" /> and triggers the private <see cref="AddGroups" /> method depending on which categories are
    /// enabled.
    /// </summary>
    public void Open()
    {
        if (_open)
            return;

        _smbios = new SMBios();
        _smbusAddress = SMBusDevice.DetectSmBusAddress(_smbios);
        Ring0.Open();
        Mutexes.Open();
        OpCode.Open();
        //{
            //byte[] buffer = new byte[4];
            //bool isOk = Ring0.ReadMemory(0xFED10000 + 0x24, ref buffer);

            //dev-test
            if (Mutexes.WaitSmBus(1222))
            {
            //const byte AcpiMmioEn = 0x24;

            //Ring0.WriteIoPort(IOMappedControlRegisters_PM_Index, (AcpiMmioEn));
            //Ring0.ReadIoPort(IOMappedControlRegisters_PM_Data);
            //uint uintValue1 = 0;
            //uint bytetValue1 = PMRead(0x00);
            //bool isOk = Ring0.ReadPciConfig(MakePciConfigAddress(0, 20, 0), 0x24, out uintValue1);
            //isOk = Ring0.WritePciConfig(MakePciConfigAddress(0, 20, 0), 0x24, 1);
            //isOk = Ring0.ReadPciConfig(MakePciConfigAddress(0, 20, 0), 0x24, out uintValue1);
            //byte value1 = PMRead(0x00);

                //byte value1 = PMRead(AcpiMmioEn);
                //byte value2 = PMRead(AcpiMmioEn + 1);
                //byte value3 = PMRead(AcpiMmioEn + 2);
                //byte value4 = PMRead(AcpiMmioEn + 3);
                ////Ring0.ReadIoPort();

                ////未理解，待查询
                //const byte smb_en = 0x00; // AMD && (Hudson2 && revision >= 0x41 || FCH && revision >= 0x49)

                //Ring0.WriteIoPort(IOMappedControlRegisters_PM_Index, (byte)(smb_en + 1));
                //byte smba_en_high = Ring0.ReadIoPort(IOMappedControlRegisters_PM_Data);

                //Ring0.WriteIoPort(IOMappedControlRegisters_PM_Index, smb_en);
                //byte smba_en_low = Ring0.ReadIoPort(IOMappedControlRegisters_PM_Data);


                //Debug.WriteLine($"smba_en_low 0x{smba_en_low.ToString("X")}");
                //Debug.WriteLine($"smba_en_high 0x{smba_en_high.ToString("X")}");
                //if (smba_en_low == 0xFF && smba_en_high == 0xFF)
                //{
                //    uint value = 0;
                //    const uint AcpiMMioAddr = 0xFED80000;
                //    const uint pmioOffset = 0x300u;
                //    byte[] buffer = new byte[4];

                //    if (Ring0.ReadMemory(AcpiMMioAddr + pmioOffset, ref buffer))
                //    {
                //        uint smbusbase = (ushort)(((value >> 8) & 0xFF) << 8);
                //        Debug.WriteLine($"smbusbase 0x{smbusbase.ToString("X")}");

                //    }


                //}


                Mutexes.ReleaseSmBus();
            }

        if (Mutexes.WaitPciBus(3333))
        {
            uint smbdev = 0, smbfun = 0;

            for (smbdev = 0; smbdev < 32; smbdev++)
            {
                for (smbfun = 0; smbfun < 8; smbfun++)
                {
                    uint value = 0;
                    if (Ring0.ReadPciConfig(MakePciConfigAddress(smbfun, smbdev, 0), PCIConfigurationOffset_VendorID, out value))
                    {
                        uint vendorID = value & 0xFFFF;
                        uint deviceID = (value >> 16) & 0xFFFF;
                        //Debug.WriteLine($"value {value}");
                        if ((vendorID & 0xFFFF) != 0xFFFF && (deviceID & 0xFFFF) != 0xFFFF)
                        {
                            Debug.WriteLine($"smbdev 0x{smbdev.ToString("X")}  smbfun 0x{smbfun.ToString("X")}");
                            const uint vendorId_AMD = 0x1022;
                            const uint deviceID_ZEN = 0x790B; // AM4/ZEN
                            if (vendorID == vendorId_AMD && deviceID == deviceID_ZEN)
                            {
                                Debug.WriteLine($"vendorID 0x{vendorID.ToString("X")}  deviceID 0x{deviceID.ToString("X")}");
                                if (Ring0.ReadPciConfig(MakePciConfigAddress(smbfun, smbdev, 0), PCIConfigurationOffset_RevisionID_and_ClassCode, out value))
                                {
                                    uint revisionID = value & 0xFF;
                                    uint classCode = (value >> 8) & 0x0FFFFFFF;
                                    Debug.WriteLine($"revisionID 0x{revisionID.ToString("X")}  classCode 0x{classCode.ToString("X")}");
                                    if (classCode == 0x0C0500) // 0C0500h denotes a SMBUS controller.
                                    {
                                        bool isOk = Ring0.ReadPciConfig(MakePciConfigAddress(smbfun, smbdev, 0), 0x24, out value);

                                        uint BaseClass = (classCode >> 16) & 0xFF;
                                        uint subClass = (classCode >> 8) & 0xFF;
                                        uint programmingInterface = classCode & 0xFF;
                                        Debug.WriteLine($"BaseClass 0x{BaseClass.ToString("X")}  subClass 0x{subClass.ToString("X")}  programmingInterface 0x{programmingInterface.ToString("X")}");
                                    }




                                }

                            }
                        }



                        //if ((vid >> 16 & 0xFFFF) != 0xFFFF)
                        //{
                        //    if (Ring0.ReadPciConfig(smbdev, 2, out did))
                        //    {
                        //        if ((did & 0xFFFF) != 0xFFFF)
                        //        {
                        //            Debug.WriteLine($"vid {vid} did {vid}");
                        //        }
                        //    }

                        //    //did = pci_config_read16(0, smbdev, smbfun, 2);
                        //    //if (did != 0xFFFF)
                        //    //{
                        //    //    Debug.WriteLine($"");
                        //    //    //if (find_smb_controller(vid, did))
                        //    //    //{
                        //    //    //    return true;
                        //    //    //}
                        //    //}
                        //}
                    }

                }
            }
            Mutexes.ReleasePciBus();
            //SpdReaderWriterCore.PciConfig.FindDeviceByClass(SpdReaderWriterCore.PciConfig.BaseClassType.Serial, SpdReaderWriterCore.PciConfig.SubClassType.Smbus, programIf: 0,);
        }
        AddGroups();

        _open = true;
    }

    private void AddGroups()
    {
        if (_motherboardEnabled)
            Add(new MotherboardGroup(_smbios, _settings));

        if (_cpuEnabled)
            Add(new CpuGroup(_settings));

        if (_memoryEnabled)
            Add(new MemoryGroup(_smbusAddress, _smbios, _settings));

        if (_gpuEnabled)
        {
            Add(new AmdGpuGroup(_settings));
            Add(new NvidiaGroup(_settings));
            Add(new IntelGpuGroup(GetIntelCpus(), _settings));
        }

        if (_controllerEnabled)
        {
            Add(new TBalancerGroup(_settings));
            Add(new HeatmasterGroup(_settings));
            Add(new AquaComputerGroup(_settings));
            Add(new AeroCoolGroup(_settings));
            Add(new NzxtGroup(_settings));
            Add(new RazerGroup(_settings));
        }

        if (_storageEnabled)
            Add(new StorageGroup(_settings));

        if (_networkEnabled)
            Add(new NetworkGroup(_settings));

        if (_psuEnabled)
            Add(new CorsairPsuGroup(_settings));

        if (_batteryEnabled)
            Add(new BatteryGroup(_settings));
    }

    private static void NewSection(TextWriter writer)
    {
        for (int i = 0; i < 8; i++)
            writer.Write("----------");

        writer.WriteLine();
        writer.WriteLine();
    }

    private static int CompareSensor(ISensor a, ISensor b)
    {
        int c = a.SensorType.CompareTo(b.SensorType);
        if (c == 0)
            return a.Index.CompareTo(b.Index);

        return c;
    }

    private static void ReportHardwareSensorTree(IHardware hardware, TextWriter w, string space)
    {
        w.WriteLine("{0}|", space);
        w.WriteLine("{0}+- {1} ({2})", space, hardware.Name, hardware.Identifier);

        ISensor[] sensors = hardware.Sensors;
        Array.Sort(sensors, CompareSensor);

        foreach (ISensor sensor in sensors)
            w.WriteLine("{0}|  +- {1,-14} : {2,8:G6} {3,8:G6} {4,8:G6} ({5})", space, sensor.Name, sensor.Value, sensor.Min, sensor.Max, sensor.Identifier);

        foreach (IHardware subHardware in hardware.SubHardware)
            ReportHardwareSensorTree(subHardware, w, "|  ");
    }

    private static void ReportHardwareParameterTree(IHardware hardware, TextWriter w, string space)
    {
        w.WriteLine("{0}|", space);
        w.WriteLine("{0}+- {1} ({2})", space, hardware.Name, hardware.Identifier);

        ISensor[] sensors = hardware.Sensors;
        Array.Sort(sensors, CompareSensor);

        foreach (ISensor sensor in sensors)
        {
            string innerSpace = space + "|  ";
            if (sensor.Parameters.Count > 0)
            {
                w.WriteLine("{0}|", innerSpace);
                w.WriteLine("{0}+- {1} ({2})", innerSpace, sensor.Name, sensor.Identifier);

                foreach (IParameter parameter in sensor.Parameters)
                {
                    string innerInnerSpace = innerSpace + "|  ";
                    w.WriteLine("{0}+- {1} : {2}", innerInnerSpace, parameter.Name, string.Format(CultureInfo.InvariantCulture, "{0} : {1}", parameter.DefaultValue, parameter.Value));
                }
            }
        }

        foreach (IHardware subHardware in hardware.SubHardware)
            ReportHardwareParameterTree(subHardware, w, "|  ");
    }

    private static void ReportHardware(IHardware hardware, TextWriter w)
    {
        string hardwareReport = hardware.GetReport();
        if (!string.IsNullOrEmpty(hardwareReport))
        {
            NewSection(w);
            w.Write(hardwareReport);
        }

        foreach (IHardware subHardware in hardware.SubHardware)
            ReportHardware(subHardware, w);
    }

    /// <summary>
    /// If opened before, removes all <see cref="IGroup" /> and triggers <see cref="OpCode.Close" />, <see cref="InpOut.Close" /> and <see cref="Ring0.Close" />.
    /// </summary>
    public void Close()
    {
        if (!_open)
            return;

        lock (_lock)
        {
            while (_groups.Count > 0)
            {
                IGroup group = _groups[_groups.Count - 1];
                Remove(group);
            }
        }

        OpCode.Close();
        InpOut.Close();
        Ring0.Close();
        Mutexes.Close();

        _smbios = null;
        _open = false;
    }

    /// <summary>
    /// If opened before, removes all <see cref="IGroup" /> and recreates it.
    /// </summary>
    public void Reset()
    {
        if (!_open)
            return;

        RemoveGroups();
        AddGroups();
    }

    private void RemoveGroups()
    {
        lock (_lock)
        {
            while (_groups.Count > 0)
            {
                IGroup group = _groups[_groups.Count - 1];
                Remove(group);
            }
        }
    }

    private List<IntelCpu> GetIntelCpus()
    {
        // Create a temporary cpu group if one has not been added.
        lock (_lock)
        {
            IGroup cpuGroup = _groups.Find(x => x is CpuGroup) ?? new CpuGroup(_settings);
            return cpuGroup.Hardware.Select(x => x as IntelCpu).ToList();
        }
    }

    /// <summary>
    /// <see cref="Computer" /> specific additional settings passed to its <see cref="IHardware" />.
    /// </summary>
    private class Settings : ISettings
    {
        public bool Contains(string name)
        {
            return false;
        }

        public void SetValue(string name, string value)
        { }

        public string GetValue(string name, string value)
        {
            return value;
        }

        public void Remove(string name)
        { }
    }
}
