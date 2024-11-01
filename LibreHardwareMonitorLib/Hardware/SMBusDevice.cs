// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Management;
using System.Threading;
using LibreHardwareMonitor.Hardware.Memory;
using static LibreHardwareMonitor.Interop.Kernel32;

namespace LibreHardwareMonitor.Hardware;

internal class SMBusDevice
{
    public SMBusDevice(ushort smbusAddress, byte slaveAddress)
    {
        _slaveAddress = slaveAddress;
        _smbusAddress = smbusAddress;
        SMBHSTSTS = (ushort)(0 + _smbusAddress);
        SMBHSTCNT = (ushort)(2 + _smbusAddress);
        SMBHSTCMD = (ushort)(3 + _smbusAddress);
        SMBHSTADD = (ushort)(4 + _smbusAddress);
        SMBHSTDAT0 = (ushort)(5 + _smbusAddress);
        SMBHSTDAT1 = (ushort)(6 + _smbusAddress);
        SMBAUXCTL = (ushort)(13 + _smbusAddress);
    }

    private int CheckPre()
    {
        ushort status = Ring0.ReadIoPort(SMBHSTSTS);
        if ((status & SMBHSTSTS_HOST_BUSY) > 0)
        {
            return -1;
        }

        status &= STATUS_FLAGS;
        if (status > 0)
        {
            Ring0.WriteIoPort(SMBHSTSTS, (byte)status);
            status = (ushort)(Ring0.ReadIoPort(SMBHSTSTS) & STATUS_FLAGS);
            if (status > 0)
            {
                return -1;
            }
        }
        return 0;
    }

    private int WaitIntr()
    {
        const int maxCount = 1000;
        int timeout = 0;
        ushort status;
        bool val;
        bool val2;

        do
        {
            status = Ring0.ReadIoPort(SMBHSTSTS);
            val = (status & SMBHSTSTS_HOST_BUSY) > 0;
            val2 = (status & (STATUS_ERROR_FLAGS | SMBHSTSTS_INTR)) > 0;

        } while ((val || !val2) && timeout++ < maxCount);

        if (timeout > maxCount)
        {
            return -1;
        }
        return status & (STATUS_ERROR_FLAGS | SMBHSTSTS_INTR);
    }

    private int CheckPost(int status)
    {
        if (status < 0)
        {
            Ring0.WriteIoPort(SMBHSTCNT, (byte)(Ring0.ReadIoPort(SMBHSTCNT) | SMBHSTCNT_KILL));
            Thread.Sleep(1);
            Ring0.WriteIoPort(SMBHSTCNT, (byte)(Ring0.ReadIoPort(SMBHSTCNT) & (~SMBHSTCNT_KILL)));

            Ring0.WriteIoPort(SMBHSTSTS, (byte)STATUS_FLAGS);
            return -1;
        }

        Ring0.WriteIoPort(SMBHSTSTS, (byte)status);

        if ((status & SMBHSTSTS_FAILED) > 0 || (status & SMBHSTSTS_DEV_ERR) > 0 || (status & SMBHSTSTS_BUS_ERR) > 0)
        {
            return -1;
        }

        return 0;
    }

    private int Transaction(byte xact) // I801: 0x0C WORD_DATA, 0x08 BYTE_DATA, 0x00 QUICK
    {
        int result = CheckPre();
        if (result < 0)
            return result;

        Ring0.WriteIoPort(SMBHSTCNT, (byte)(Ring0.ReadIoPort(SMBHSTCNT) & ~SMBHSTCNT_INTREN));
        Ring0.WriteIoPort(SMBHSTCNT, (byte)(xact | SMBHSTCNT_START));

        int status = WaitIntr();

        return CheckPost(status);
    }

    public ushort ReadWord(byte register)
    {
        if (_smbusAddress == 0 || _slaveAddress == 0)
            return 0xFFFF;

        Ring0.WriteIoPort(SMBHSTADD, (byte)(((_slaveAddress & 0x7F) << 1) | (SMB_READ & 0x01)));
        Ring0.WriteIoPort(SMBHSTCMD, register);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & (~SMBAUXCTL_CRC)));

        Transaction(0x0C);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & ~(SMBAUXCTL_CRC | SMBAUXCTL_E32B)));

        return (ushort)(Ring0.ReadIoPort(SMBHSTDAT0) | (Ring0.ReadIoPort(SMBHSTDAT1) << 8));
    }

    public byte ReadByte(byte register)
    {
        if (_smbusAddress == 0 || _slaveAddress == 0)
            return 0xFF;

        Ring0.WriteIoPort(SMBHSTADD, (byte)(((_slaveAddress & 0x7F) << 1) | (SMB_READ & 0x01)));
        Ring0.WriteIoPort(SMBHSTCMD, register);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & (~SMBAUXCTL_CRC)));

        Transaction(0x08);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & ~(SMBAUXCTL_CRC | SMBAUXCTL_E32B)));
        return Ring0.ReadIoPort(SMBHSTDAT0);
    }



    public void WriteWord(byte register, byte value)
    {
        if (_smbusAddress == 0 || _slaveAddress == 0)
            return;

        Ring0.WriteIoPort(SMBHSTADD, (byte)(((_slaveAddress & 0x7F) << 1) | (SMB_WRITE & 0x01)));
        Ring0.WriteIoPort(SMBHSTCMD, register);
        Ring0.WriteIoPort(SMBHSTDAT0, (byte)(value & 0x00FF));
        Ring0.WriteIoPort(SMBHSTDAT1, (byte)((value & 0xFF00) >> 8));

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & (~SMBAUXCTL_CRC)));

        Transaction(0x0C);

        
        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & ~(SMBAUXCTL_CRC | SMBAUXCTL_E32B)));
    }

    public void WriteByte(byte register, byte value)
    {
        if (_smbusAddress == 0 || _slaveAddress == 0)
            return;

        Ring0.WriteIoPort(SMBHSTADD, (byte)(((_slaveAddress & 0x7F) << 1) | (SMB_WRITE & 0x01)));
        Ring0.WriteIoPort(SMBHSTCMD, register);
        Ring0.WriteIoPort(SMBHSTDAT0, value);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & (~SMBAUXCTL_CRC)));

        Transaction(0x08);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & ~(SMBAUXCTL_CRC | SMBAUXCTL_E32B)));
    }

    public bool checkDevice()
    {
        Ring0.WriteIoPort(SMBHSTADD, (byte)(((_slaveAddress & 0x7F) << 1) | SMB_WRITE));
        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & (~SMBAUXCTL_CRC)));

        int res = Transaction(0x00);

        Ring0.WriteIoPort(SMBAUXCTL, (byte)(Ring0.ReadIoPort(SMBAUXCTL) & ~(SMBAUXCTL_CRC | SMBAUXCTL_E32B)));

        return res >= 0;
    }

    // returns smbus address
    public static ushort DetectSmBusAddress(SMBios smbios)
    {
        try
        {
            if(smbios.Processors.Length == 0)
            {
                return 0;
            }
            ProcessorInformation processorInformation = smbios.Processors[0];

            if(processorInformation.ManufacturerName == "Advanced Micro Devices, Inc.")
            {
                foreach (var device in smbios.MemoryDevices)
                {
                    if (device.Type == MemoryType.DDR5)
                    {
                        return 0x0B00;
                    }
                }
            }
            else
            {
                return 0;
            }


        }
        catch { }

        return 0;
    }

    private readonly ushort _smbusAddress = 0;
    private readonly byte _slaveAddress = 0;

    private ushort SMBHSTSTS = 0;
    private ushort SMBHSTCNT = 0;
    private ushort SMBHSTCMD = 0;
    private ushort SMBHSTADD = 0;
    private ushort SMBHSTDAT0 = 0;
    private ushort SMBHSTDAT1 = 0;
    private ushort SMBAUXCTL = 0;

    private const byte SMB_READ = 0x01;
    private const byte SMB_WRITE = 0x00;

    private static ushort SMBAUXCTL_CRC = (1 << 0);
    private static ushort SMBAUXCTL_E32B = (1 << 1);

    private static ushort SMBHSTCNT_INTREN = (1 << 0);
    private static ushort SMBHSTCNT_KILL = (1 << 1);
    //private static ushort SMBHSTCNT_LAST_BYTE = (1 << 5);
    private static ushort SMBHSTCNT_START = (1 << 6);
    //private static ushort SMBHSTCNT_PEC_EN = (1 << 7);

    private static ushort SMBHSTSTS_BYTE_DONE = (1 << 7);
    //private static ushort SMBHSTSTS_INUSE_STS = (1 << 6);
    //private static ushort SMBHSTSTS_SMBALERT_STS = (1 << 5);
    private static ushort SMBHSTSTS_FAILED = (1 << 4);
    private static ushort SMBHSTSTS_BUS_ERR = (1 << 3);
    private static ushort SMBHSTSTS_DEV_ERR = (1 << 2);
    private static ushort SMBHSTSTS_INTR = (1 << 1);
    private static ushort SMBHSTSTS_HOST_BUSY = (1 << 0);

    private static ushort STATUS_ERROR_FLAGS = (ushort)(SMBHSTSTS_FAILED | SMBHSTSTS_BUS_ERR | SMBHSTSTS_DEV_ERR);
    private static ushort STATUS_FLAGS = (ushort)(SMBHSTSTS_BYTE_DONE | SMBHSTSTS_INTR | STATUS_ERROR_FLAGS);


    private const byte Register_MR11 = 11;
}
