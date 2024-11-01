// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware.Motherboard;

namespace LibreHardwareMonitor.Hardware.Memory
{
    internal class Spd5Hub
    {
        readonly byte _i2cAddress;

        public SPD5DeviceType DeviceType;

        public byte Hid;

        public enum SPD5DeviceType:ushort
        {
            SPD5118 = 0x1851,
            SPD5108 = 0x0851,
        }

        const byte REGISTERADDRESS_MR11 = 11; // I2C Legacy Mode Device Configuration
        const byte REGISTERADDRESS_MR26 = 26; // TS Configuration
        const byte REGISTERADDRESS_MR48 = 48; // Device Status
        const byte REGISTERADDRESS_MR49 = 49; // TS Current Sensed Temperature - Low Byte
        const byte REGISTERADDRESS_MR50 = 50; // TS Current Sensed Temperature - High Byte


        SMBusDevice _slave;
        Spd5Hub(SMBusDevice slave, byte hid, SPD5DeviceType deviceType) {
            _slave = slave;
            Hid = hid;
            DeviceType = deviceType;
        }

        public static Spd5Hub DetectSpd5Hub(ushort smbusAddress, byte hid)
        {
            List<Spd5Hub> list = new List<Spd5Hub>();

            byte slaveAddress = (byte)((0x50 | hid) & 0xFF);
            SMBusDevice slave = new(smbusAddress, slaveAddress);

            bool checkDevice = false;
            if (Mutexes.WaitSmBus(100))
            {
                checkDevice = slave.checkDevice();
                Mutexes.ReleaseSmBus();
            }

            if (checkDevice)
            {
                //MR0 MR1  
                SPD5DeviceType deviceType = (SPD5DeviceType)slave.ReadWord(0x00);
                if (deviceType == SPD5DeviceType.SPD5118 || deviceType == SPD5DeviceType.SPD5108)
                {
                    return new Spd5Hub(slave, hid, deviceType);
                }
            }

            return null;
        }


        public static List<Spd5Hub> DetectAllSpd5Hub(ushort smbusAddress)
        {
            List<Spd5Hub> list = new List<Spd5Hub>();

            for(byte hid = 0; hid < 8; hid++)
            {
                Spd5Hub spd5Hub = DetectSpd5Hub(smbusAddress, hid);
                if(spd5Hub != null)
                {
                    list.Add(spd5Hub);
                }
            }
            return list;
        }

        private static Int32 SignExtend32(Int32 value, Int32 index)
        {
            Int32 shift = 31 - index;
            return (value << shift) >> shift;
        }

        public bool SetThermalSensorConfiguration(bool disableThermalSensor)
        {
            if (DeviceType != SPD5DeviceType.SPD5118)
                return false;

            if (!Mutexes.WaitSmBus(100))
                return false;

            bool result = false;

            byte thermalSensorConfiguration = Register_ReadByte(REGISTERADDRESS_MR26);

            if(((thermalSensorConfiguration & 0x01) == 0x01) != disableThermalSensor)
            {
                thermalSensorConfiguration = (byte)((thermalSensorConfiguration & 0xFE) | (disableThermalSensor? 0x01 : 0x00));

                Register_WriteByte(REGISTERADDRESS_MR26, thermalSensorConfiguration);

                result = Register_ReadByte(REGISTERADDRESS_MR26) == thermalSensorConfiguration;
            }
            Mutexes.ReleaseSmBus();

            return result;
        }

        public bool GetTemperature(ref float temperature)
        {
            if(DeviceType != SPD5DeviceType.SPD5118)
                return false;

            if (!Mutexes.WaitSmBus(50))
                return false;

            ushort data = Register_ReadWord(REGISTERADDRESS_MR49);
            temperature = SignExtend32(data >> 2, 10) * 0.25f;

            Mutexes.ReleaseSmBus();

            return true;
        }


        public string GetModulePartNumber()
        {
            byte[] buffer = new byte[0x226 - 0x209 + 1 + 1];

            int i = 0;

            if (!Mutexes.WaitSmBus(100))
                return string.Empty;
            for (ushort address = 0x209; address <= 0x226; address++)
            {
                byte value = Eeprom_ReadByte(address);
                if (value == 0)
                {
                    break;
                }
                buffer[i++] = value;
            }
            Mutexes.ReleaseSmBus();

            string modulePartNumber = System.Text.ASCIIEncoding.Default.GetString(buffer);

            return modulePartNumber;
        }


        public string GetModuleSerialNumber()
        {
            byte[] buffer = new byte[0x208 - 0x205 + 1];
            int i = 0;

            if (!Mutexes.WaitSmBus(100))
                return string.Empty;
            for (ushort address = 0x205; address <= 0x208; address++)
            {
                byte value = Eeprom_ReadByte(address);
                buffer[i++] = value;
            }
            Mutexes.ReleaseSmBus();
            Array.Reverse(buffer);
            string modulePartNumber = BitConverter.ToUInt32(buffer, 0).ToString("X8");
            
            return modulePartNumber;
        }


        private byte Register_ReadByte(byte registerAddress)
        {
            return _slave.ReadByte(registerAddress);
        }

        private void Register_WriteByte(byte registerAddress, byte value)
        {
            _slave.WriteByte(registerAddress, value);
        }

        private ushort Register_ReadWord(byte registerAddress)
        {
            return _slave.ReadWord(registerAddress);
        }

        private bool Eeprom_SetPage(byte addressPagePointer)
        {
            if (((addressPagePointer >> 4) & 0x0F) != 0)
            {
                return false;
            }

            if ((addressPagePointer & 0x08) != 0x00)
            {
                return false;
            }

            byte registerValue = addressPagePointer;
            _slave.WriteByte(REGISTERADDRESS_MR11, registerValue);
            return true;
        }

        private byte Eeprom_ReadByte(ushort eepromAddress)
        {
            byte addressPagePointer = (byte)(eepromAddress / 128);
            Eeprom_SetPage(addressPagePointer);

            byte offset = (byte)(eepromAddress & 0x7F);
            offset |= 0x80;

            return _slave.ReadByte(offset);
        }
    }


}
