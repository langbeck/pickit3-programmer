using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices; // DllImport
using Pk2 = PICkit2V2.PICkitFunctions;
using P32 = PICkit2V2.PIC32MXFunctions;
using PE33 = PICkit2V2.dsPIC33_PE;
using PE24 = PICkit2V2.PIC24F_PE;
using KONST = PICkit2V2.Constants;
using UTIL = PICkit2V2.Utilities;
using Pk3h = PICkit2V2.PK3Helpers;

// version 2.01.00  -  18 Sept 2006 WEK
// Initial release 
//
// version 2.10.00  -  5 December 2006 WEK
// Bug Fix: When opened with no PICkit 2, clicking Tools->Code Protect would cause a crash in UpdateGUI
//          due to pk2.DeviceBuffers being NULL.
//          FIXED by adding a call to Pk2.ResetBuffers() in public FormPICkit2()
// Bug Fix: Error importing User IDs for dsPIC33 / PIC24H.  Changed hex import routine for user IDs
//          line "int uIDshift = (bytePosition / userIDMemBytes) * userIDMemBytes;" to include "* userIDMemBytes".
//          Similar change made to EEPROM import.
// Bug Fix: Write/Read/Verify/BlankCheck routines were only checking for the first 0x8000-word boundary of the 
//          PIC24/dsPIC33(0x10000 address) at which the TBLPAG needed to be updated.  Now checks all boundaries.
//          (Affects "256" memory size parts.)
// Bug Fix: In SetVDDVoltage, now does not let error threshold get set above 4.0V.  There is more droop at
//          4.7V to 5.0V (typical) so threshold was getting set relatively higher at these voltages.
// Bug Fix: In SetOSCCAL.clickset(), added try-catch as it would throw an exception when the edited text was
//          less than 2 characters.
// Bug Fix: ImportHexFile() now applies the config word mask to config words in Program Memory as they are imported.
//          Bits set outside the mask formerly appeared in program memory, and during a manual "verify" could cause
//          verification error if the bits are read-only "zero".
// Change:  Disabled Maximize box on main form.
// Feature: Updated Export Hex routine to handle exporting config locations of the same length as the memory 
//          locations and config locations that span more than 1 hex line.
// Feature: Added "slow" programming mode value to INI file, so it can be edited by a user.
// Feature: Turning off "Fast Programming" now also doubles all script delays.
// Feature: Added part parameter "BlankCheckSkipUsrIDs" to device file.  Blank Check will not check UserIDs 
//          for blank if true.  This is because dsPIC33FJ/PIC24H do not erase UserIDs on bulk erase, and MPLAB
//          does not write them to a blank value on erase.
// Feature: Changed method deviceFamilyClick so it now defaults the settable VDD range to the range for the 
//          first device file part in the selected family if no device is found.  Previously, it would set
//          all families to the "safe" range of 2.7 - 3.3V.
// Feature: Added "Help -> PICkit 2 on the web"
// Feature: Added "Tools -> Troubleshoot..." wizard
// Feature: Added "Programmer -> Hold Device in Reset" and /MCLR checkbox.  Now sets VPP pin to appropriate
//          /MCLR state after programming. (active low or tri-state).
// Feature: Now updates the EEPROM window immediately after EE read on PIC18F writes where it has to read/restore
//          EE memory. (added a this.refresh())
// Feature: Added test memory support for baseline, midrange, & PIC18F.
// Feature: Added shortcut keystrokes to some menu items.
// Feature: Added Auto Import-Write functionality to Import + Write button on bottom right of form.
// Feature: Added testing support through dll.
//
// version 2.10.01  -  22 January 2007 WEK
// Bug Fix: If a long string of characters was entered while editing a Program Memory or Data EEPROM cell, it
//          would cause an exception in the convert utility.  Added code to progMemEdit & eEpromEdit so an 
//          exception just causes the value to be set to zero.
// Bug Fix: When Tools > Target VDD Source > Force Target is selected and AutoImportWrite is being used, all
//          MessageBox dialogs for VDD/VPP errors are suppressed as they were creating some kind of exception
//          which cause an infinite loop of Write attempts and PICkit 2 to lock up.
// Change:  When in AutoImportWrite mode, Target VDD status change dialogs are suppressed.
// Change:  All references to "firmware" in user dialogs changed to "Operating System".
// Feature: Added link to 44-pin demo board user's guide under Help menu.
// Feature: "Write on PICkit button" state now saved in INI file.
// Feature: When Auto-Import-Write fails, the button is now left enabled to more easily retry.
//
// version 2.11.00  -  5 February 2007 WEK
// Bug Fix: For baseline/midrange hex files imported with CP bit(s) asserted, checksum is now correctly computed
// Bug Fix: For PIC18F hex files imported with ALL CP bits asserted, checksum is now correct.  If only SOME CP bits
//          are asserted, the checksum will not match MPLAB
// Change:  Help menu links point to new revisions of PICkit 2 UG and 44p DB UG
//      Device File changes in 1.12:
//              Imports/Exports new PIC24HJ and dsPIC33 HEX file User ID format
//              Fix CP Masks for PIC24HJ and dsPIC33 parts.
//
// version 2.20.00 - 27 February 2007 WEK
// Bug Fix: Changed "checkEraseVoltage" method to shut off auto-import-write timer while displaying the erase
//          voltage dialog else it seems to repeatedly try to display the dialog on auto-import-writes resulting
//          in an exception.
// Bug Fix: Added calls to Pk2.SetVDDVoltage on VDD "On" checkbox call and preprogramming check to ensure
//          VDD voltage is set to expected value, especially if PICkit 2 gets reset somehow.
// Change:  Upped Device File Compatability to '2' due to requirements for handling dsPIC30F devices.
// Feature: Added ability to write Calibration Words in Configuration(Test) Memory for Mid-Range parts
//          to Test Memory window.
// Feature: Changes to code downloading address for EEWrPrep scripts for dsPIC30 support.
// Feature: Changes to hex import code to ignore hex data from device file fields Ingore Address and IgnoreBytes
// Feature: Add new dialog to display dsPIC30 Unit IDs and button to open dialog.  Code changes to support it were
//          also made to UpdateGUI.
// Feature: Added support for ChipErasePrepScripts.
//
// version 2.20.01 - 14 March 2007 WEK
// Bug Fix: In UpdateGUI, sets buttonShowIDMem invisible if UserIDs < 0.  Wasn't previously, so button was
//          was still visible if switching from a dsPIC30F to a PIC18F_J_ for example.
// Bug Fix: The number of process handles was going up very quickly and steadily when "Program on button" was
//          selected, causing some systems to bog down.  Removed the PICkit 2 detection from "timerGoesOff" method-
//          it will still be detected if PICkit 2 unplugged and prevents the unchecked increase in handles.
// Bug Fix: Improved /MCLR handling during programming - make sure code doesn't execute between VDD applied and first
//          program mode entry.  Added SetMCLRTemp in PICkitFunctions.cs (and many calls to it).
//
// version 2.20.03 - 3 April 2007 WEK
// Feature: Changes to support Baseline dataflash-
//              readEEPROM() to use 0xFFF mask for baseline parts with dataflash; created getEEBlank().
//              updateGUI() to display half columns and 3 nibbles for baseline with dataflash
//              DeviceData() constructor to initialize EE data to 0xFFF for baseline dataflash.
//              writeDevice() to use proper EE mask
//              deviceVerify() to use proper EE mask
//              blankCheckDevice() to use proper EE mask
// 
// version 2.20.04 - 3 April 2007 WEK
// Bug Fix: Left in the Read Debug Vector test line in deviceRead()!!  Now commented out.
//          
// version 2.20.05 - 12 April 2007 WEK
// Bug Fix: Change to deviceWrite() to fix WRTC config bit asserted preventing writing of CONFIG7 in PIC18F/K parts.
//          
// version 2.20.07 - 19 April 2007 WEK
// Feature: Added support for Row Erase scripts to be read and downloaded. Several changes to deviceWrite()
//          and other functions to support erasing a part using row erases when VDD is below Verase.
//          NOTE that "Erase" button still uses only bulk erase, so won't erase below Verase.
//          This version only contains support for Midrange and PIC18F row erase, not dsPIC30F
// 
//
// version 2.30.00 - 15 May 2007 WEK
// Feature: Added "scriptRedirectTable" to PICkitFunctions.cs.  Includes changes to RunScriptxxxxxx methods,
//          downloadScript, & downloadPartScripts.  The new functionality recognizes the same script used
//          in different script locations, and redirects subsequent locations to the original, so the script
//          isn't downloaded multiple times to the script buffer.  This saves space in the script buffer and
//          is necessary for dsPIC30F as all scripts including repeats won't fit in the buffer otherwise.
// Feature: Added code to RowEraseDevice() to handle row erasing EE in dsPIC30 devices for low voltage erase
// Feature: Hex Import/Export: if a device has EE Data, (and a different device than the current is not
//          detected during the Import process) then only the regions enabled by the region checkboxes
//          are imported/exported.
// Feature: setGUIVoltageLimits() now attempts to set a nominal voltage - ex 3.3V for 2.5 - 3.6 V parts.
// Feature: In memory display drop-down boxes, changed "Hex+ASCII" to "Word ASCII" and added "Byte ASCII".
//          "Word ASCII" displays as "Hex+ASCII" did, where each ASCII character is displayed in the same 
//          position as the byte in the memory word
//          "Byte ASCII" displays the ASCII characters in increasing byte order, so strings appear readable.
// Change:  ExecuteScript is now public. ConfigMemEraseScript is no longer downloaded to the script buffer
//          instead it is always directly executed with ExecuteScript. (Needed to make room in buffer for dsPIC30)
// Change:  TestMemWriteScript and words (which was never used) is now EERowEraseScript & words.
// Change:  Change constant VddThresholdForSelfPoweredTarget to 2.3V.
// Bug Fix: In SetVDDVoltage, don't allow voltage to be set below minimum of 2.5.  With Force VDD target,
//          an operation attempt without a target voltage (or with VDD pin not connected) could cause VDD
//          to get set to a very low value, e.g. 0.234 V, which would prevent the VPP pump from working.
// Bug Fix: Changed eEpromEdit() to use getEEBlank() to properly mask edits on 12F519 "EEPROM" data flash.
// Bug Fix: If no device was detected on startup, or the current device has no EEPROM, then a "Read" or
//          "Import Hex" operation on a new connected part with EEPROM would fail to read or import the
//          EEPROM, because the checkbox wouldn't be checked until after the operation.  Fix with change
//          to preProgrammingCheck() that checks the box if it is currently disabled and part has EE.
//
//
// version 2.40.00 - 25 Jun 2007 WEK
// Feature: Added menu option "Tools > Use VPP First Progam Entry" option.  When selected, will disable
//          "Target VDD Source" and set selection to "Force PICkit 2".
// Feature: Added support for KEELOQ HCS parts.
// Feature: Added support for 24LC, 25LC, & 93LC serial EEPROMS.
// Feature: Added ini value SETV which saves the VDD setting.  If the family on startup is the same as LFAM,
//          then it will be restored to the VDD set box on startup.
// Feature: Added ability to group device families in submenus under the "Device Family" menu.  Families
//          in the device file with the same submenu name will be grouped together.  Families to belong
//          to submenus are named as "submenu/familyname" where "/" separates the submenu name from the
//          the family name.
//          As a result, familyMenuTable is no longer used.  The device is searched for the family name
//          instead.
// Feature: Added INI value "PDET" which allows searching for parts (auto-detecting) on application startup
//          to be turned off.
// Feature: Added ability to Calibrate PICkit 2 and assign Unit ID.  "Tools > Calibrate VDD & Set Unit ID" item
//          added, and DialogCalibrate.  Unit ID will be displayed in Status Window and main form title bar if
//          assigned when app is opened or "Check Communications" is selected.
//          The menu item is not available if the INI file contains an "EDIT: N" entry.
// Feature: In UpdateGUI() now checks for validity of OSCCAL instructions in program memory.
// Feature: WRITE and ERASE will warn the user of an invalid OSCCAL and give them the option to abort
//          the operation.
// Feature: UART Tool added.
// Bug Fix: In main form constructor, FormPICkit2(), now will disable all controls if the active family
//          is not PartDetect.  Previously, READ, ERASE, BLANK CHECK were enabled, and would crash the app
//          if clicked before a device was selected.
// Bug Fix: In ExportHexFile program memory section, now also checks arrayIndex against program memory
//          length to prevent unnecessary segment address at end of file.
// Bug Fix: "Troubleshoot..." menu now disabled if PICkit 2 not found.
//
//
// version 2.50.00 - 10 April 2008 WEK
// Feature: Updated FormTestMemory.cs to use the DevFile.Families[Pk2.GetActiveFamily()].TestMemoryStart
//          value to key off of instead of the Family ID for test memory support.  This was part of
//          supporting 200K midrange parts.
// Feature: Change to searchDevice() in PICkitFunctions.cs (along with devFile change) to read
//          16-bit silicon revisions and display with REVS.
// Feature: At startup and via Check Communications allows selection from all connected PICkit 2 units.
//          UnitID is used to differentiate.  MUST use firmware 2.30 or later to connect multiple units
//          or will crash machine.  (update 1 at a time).  Dialog Unit# = 0 will be used by MPLAB.
//          Unplugging other units may cause the current unit to not be found. Use Check Comm to reselect.
// Feature: Added "SDAT:" INI file label to activate new DialogDevFile to select a device file.
// Feature: Now allow VDD on with UART Tool.
// Feature: Now can select and copy sections of Memory windows.
// Feature: Detects and warns if some config words are not defined in HEX file.
// Feature: "Code/Data/All Protect" is now displayed based on the settings in the loaded/read Configuration.
//          If loaded hex/read data has CP and/or DP asserted, label displays appropriately, menu protect
//          option is checked and disabled (can't "undo" hex protection)
// Feature: Can "Hide" Device File families by adding a pound '#' character to the start of the family name.
//          Also be sure to turn off "Part Detect" for that family.
// Feature: Includes support for 11LC EEPROM family devices.
// Feature: PK2 window remembers its last location
// Feature: Added View menu with multi-window view.  (if window view error happens & can't fix it, delete INI file).
// Feature: PIC32 support - note ASCII views not supported, Fast Programming always used
// Feature: Programmer-To-Go support.
// Feature: Added optional Manual Device Select
// Feature: MCP250xx CAN device support - MCP is OTP!!!!  Also, Must be powered from PICkit 2!!!
// Feature: Logic Tool
// Feature: Changed platform to "x86" to support Vista 64
// Change:  Changed the way PIC18J device config words are handled to match MPLAB: now uppper nibble is
//          always set to ones, and "blank" value is masked and written on an Erase.
// Change:  Changed the way PIC24FJ config words are handled to match MPLAB: Upper byte now always set to
//          ones, and unimplemented bits are shown as one's in the Program Memory window.
// Change:  Device file compatability level changed to 5.
// Change:  Includes User Guide Rev E
// Bug Fix: In PICkitFunctions.cs, changed ReadDeviceFile() to use File.OpenRead instead of File.Open
//          as the file only needs to be read, and opening it for read/write causes problems when run
//          on read-only media (like CD).
// Bug Fix: Was crashing on application close when no device file is found.  Fixed with check in SaveINI()
//          for no device file.
// Bug Fix: DPI- Addressed non-normal (96) DPI settings with scalefactW and scalefactH, which are used to scale
//          all pixel Width and Height constants.
// Bug Fix: When attached PICkit 2 is in Bootloader Mode, the Help->About dialog now correctly reports the
//          Firmware Version as 'BL' with the bootloader version.  Also, Help->About dialog now has X close box
//          for display situations where the "OK" button is not visible.
// Bug Fix: Fixed issue where part with OSCCAL "Invalid Value" displayed still displayed "Invalid Value" when
//          switching to another family or part without OSCCAL.
// Bug Fix: Test Memory for Baseline devices with Dataflash is now handled properly.
// Bug Fix: MCLR checkbox now disabled by default if Pk2 not found.
// Bug Fix: Change to initializeGUI to fix issue where view mode was always reset to "Hex Only"
// Bug Fix: (Device File) PIC24H/dsPIC33F now always disable JTAGEN config bit
// Bug Fix: "Write on PICkit Button" now doesn't program endlessly on single button press.
// Bug Fix: For 16F5x baseline devices, sets config bits 13 to 4 on hex file import, to prevent configuration
//          verify errors.
// Bug Fix: Fixed issue with detection of PIC18K devices corrupting/erasing portions of midrange devices (fix in device file.)
//
//
// version 2.50.01 - 16 April 2008 WEK
// Bug Fix: Requires firmware v2.30.01 which fixes an issue with 24LC EEPROM reads.
//
// version 2.50.02 - 2 May 2008 WEK
// Bug Fix: Prevents windows from opening off the display by limiting location values from INI.
// Bug Fix: Disables Tools > UART Tool and Tools > Logic Tool when no PK2 unit is available.
// Bug Fix: Fixed a problem with updating the OS with multiple PICkit 2's:  Downloading was failing if the PICkit 2
//          unit numbering changed when one was entered into BL mode.  Now looks for the unit in BL mode, and brings
//          up unit select dialog after updating.
// Bug Fix: Changes to FamilySelectLogic() to prevent ProgramMem window opening before main window when manual device
//          select enabled.
//
//
// version 2.51.00 - 6 June 2008 WEK
// Feature: Added VDD control checkbox to UART Tool dialog.
// Feature: Added VDD control checkbox to Logic Tool dialog.
// Bug Fix: Add sleep(100) delays to DownloadPE() in PIC32MXFunctions to prevent "download PE FAILED" error seen by 
//          customer.
// Bug Fix: Fixed problem with PTG hangups at crossover from first EEPROM to second.  ENTER_LEARN_MODE now includes
//          memory size (same as ENABLE_PK2GO_MODE) for the FW to properly set the value during download.
// Bug Fix: Fixed PTG mode exit problem where "junk" was read from the device - need to always download scripts when
//          the script buffer checksum is 0.
// Bug Fix: Fixed DialogCustomBaud frame size issue.
// Bug Fix: Now displays "Done" when reading PIC32 devices is complete.
//
//
// version 2.52.00 - 15 July 2008 WEK
// Feature: Added Tools > Clear Memory Buffers on Erase.  May be unchecked so buffers aren't cleared on an Erase.
//          Saved in INI file as CLBF.
// Bug Fix: Fixed issue with multiple PK2 support caused by reading FW version and Unit ID while searching for
//          available PK2 units.  Reading the FW version and UnitID via HID transactions may break communications
//          between software already opened and a PICkit 2 unit it is connected to.  To fix this, FW version 
//          2.32.00 or later is required, which returns the Unit ID in the device USB Serial Number String 
//          descriptor instead.  This allows the UnitID to be read without a HID transaction.  However, whenever
//          the Unit ID is changed the PICkit 2 must be reset so it re-enumerates on USB with the new Unit ID
//          in the descriptors.
// Bug Fix: No longer crashes when programming PIC32 with completely blank Boot Flash.
// Bug Fix: Programmer-To-Go no longer fails verify when dsPIC33/PIC24HJ Config8 is not in device file, as
//          this caused the JTAGE bit to be set.  Now applies mask to Config 8 to ensure JTAG bit is clear.
//
//
// version 2.55.00 - 15 Sept 2008 WEK
// Feature: Added dsPIC33_PE.cs to support using programming executive for PIC24H/dsPIC33F devices.  This also
//          includes the workaround for the Device ID corruption issue.  'PE33' in the INI file may be used
//          to disable PE support and revert to ICSP (note that ICSP does not protect against Device ID errata.)
//          NOTE: Pogrammer-To-Go does not use the PE.
//          NOTE: PE is never used for devices with < 4096 instruction locations.
// Feature: Added PIC24F_PE.cs to support using programming executive for PIC24F devices.  'PE24' in the INI file
//          may be used to disable PE support and revert to ICSP.
//          NOTE: Pogrammer-To-Go does not use the PE.
// Feature: UART Tool now allow typing of hex directly for transmission.  In hex mode, when the display is active,
//          typing a hex nibble (0-F) displays this first nibble "Type Hex : 0_" below right of the display.  When
//          a second nibble is typed, the byte is sent out TX.  Pressing any non-hex key or clicking any other
//          after the first nibble is typed will cancel the first nibble.
// Feature: Programmer > Alert Sounds... brings dialog to select and enable playing of WAV files on success,
//          warning, or error events in status window.  Settings saved in INI file.  Default sounds in application
//          folder "Sounds" subdirectory.
// Feature: Allows import/export of BIN binary files for serial EEPROM devices.
// Change:  Due to a firmware error in FW v2.32, only up to 14 characters, not 15, of the Unit ID are reported.
//          Even with 14, the last character is not properly terminated with 0x00.  Thus the Unit ID assignment
//          dialog limits the Unit ID assigned to 14 characters now, and the USB.cs file limits reading of the
//          Unit ID to 14 characters max.
// Bug Fix: Adjusted the timing for Troubleshoot dialog "30kHz" PGx toggles - firmware v2.3x reduced the inherent
//          timing of the loop due to the script engine change from a C Switch statement to assembly jump tables.
//          Timing is now nominally 28kHz.
// Bug Fix: (Device File) - fixed PIC32MX4xx config mask which didn't have USB bits.
// Bug Fix: (Device File) - PIC18 program memory read script now reads the first byte of every 64 word block
//          twice.  The first read is thrown away.  This prevents the Table Read Protect issue from showing up
//          on subsequent reads and verifies.
// Bug Fix: Fix an issue with UART Tool in HEX mode where if no "RX:" breaks are found and data keeps coming, the
//          dialog locks up after reaching the 16K character limit.  Fixed with check for max character count in
//          timerPollForData_Tick().  This should also prevent similar issues in ASCII mode.
//
// version 2.55.01 - 22 September 2008 WEK
// Bug Fix: UART Tool fast polling time set back to 15ms.  Was changed (intended to be temporary) during debug of
//          UART Tool bug fixed in 2.55.00
//
// version 2.55.02 - 9 January 2009 WEK
// Bug Fix: Updated the PIC32 PE from v0106 to v0109, which fixes verify issues with some parts, such as PIC32MX320
//          devices.
//
// version 2.60.00 - 30 January 2009 WEK
// Feature: Improved PIC18F6xJxx, 8xJxx program times (device file)
// Feature: New menus Tools > Display Unimplemented Config Bits > (As '0' bit value, As '1' bit value, As read or imported)
//          Allows user to select how configuration words are shown.  "As '0' bit value" is default and how words
//          were displayed in previous versions.  New INI file "CFGU" item to save selection.
// Feature: Configuration editor (DialogConfigEdit).  Disabled if INI file "EDIT: N".
// Feature: Added Tools > Use LVP Program Entry.  This allows devices supporting LVP to use it.  Restrictions:
//          Can only be used in "Manual Device Select" mode, and cannot be used if "VPP First" is enabled.
//          Works with Programmer-To-Go.
// Feature: In Manual Device Select mode, if the selected part is from a normally auto-detect family
//          the Device ID will be checked to ensure the correct device on any programming operation.
//          This does NOT include file import.
//          The INI file entry DVER: can be changed to 'N' to disable this feature.
// Feature: 9th configuration word support for PIC24F-KA- devices.
// Change:  The PE program entry scripts in dsPIC33_PE.cs and PIC24F_PE.cs were changed to pulse MCLR high for
//          500us prior to entering the 32-bit sequence (from 5ms) to comply with a tool standardization on this
//          due to issues with devices executing code during this pulse.
// Change:  ImportHexFile() no longer applies configuration masks to imported configuration data.  This is to support
//          the new display feature above.
// Change:  FamilyIsPIC24F() is now FamilyIsPIC24FJ() to prevent confusion with PIC24F-KA parts.
// Bug Fix: ComputeChecksum updated so Baseline and Midrange checksums are computed correctly when CP and/or DP
//          is enabled (in GUI menu as well.)
// Bug Fix: Deprecating RunScriptUploadNoLen2() - this may be a cause of lockup issues on USB chipsets that don't
//          handle blocking reads very well.  This single command requested 2 reads, to reduce the number of USB
//          commands during a read.  Adding an extra USB read command may slow reads of some device types by as
//          much as 15%, others won't be noticably slower.
// Bug Fix: Device silicon revision was sometimes being displayed with junk in the upper word value, which should
//          always be clear (due to families that only returned 2 bytes instead of 4).  So now explicitly clears
//          this in searchDevice().
// Bug Fix: ImportHexFile() initialization of configLoaded[] changed so that when there are no implemented bits in
//          in a config word, it does not warn if the word is not in the hex file.  (Warning on an unused config
//          location created confusion.)
// Bug Fix: Alert Sounds default location issue fixed so sounds will work if not installed in default location on
//          C: drive.
//          PIC24FJ0xx devices blank config word 3 to 7FFF to match MPLAB (was 3FFF).
// Bug Fix: Fix progress bar issue with PIC24F and dsPIC33 PEs during Writes and PIC24F Verify.  Changes to
//          PE24FWrite() PE24FVerify() PE33Write() 
// Bug Fix: Fix an issue with PIC24FJ code that modifies program memory causing verify fail.  Fixes by making sure
//          to stay in programming mode between write and verify.
// Bug Fix: Fixed an issue where Use VPP First Program Entry select / deselect may not have an effect in Manual
//          Device Select mode.
// Bug Fix: Sometimes "Fail" sound was played when Programmer-To-Go finished downloading, even though it was
//          successful.  This was a verify of config failure causing this.  It no longer fails verify of config
//          when in learn mode.
// Bug Fix: Instead of disabling the entire "programming" menu, only the functions requiring a chip be selected
//          in Manual Device Select mode are disabled.  This allows Manual select mode to be turned off without
//          having to select a device first.
//
// NOTE: PIC24FJ "Blank" config values do not match MPLAB as PICkit 2 always programs them with JTAG disabled.
//
// version 2.61.00 - 23 March 2009 WEK
// Bug Fix: PIC18F97J60 bug in device file released with v2.60 now fixed
// Bug Fix: Fix a problem with PIC24FJ programming introduced in v2.60 where Verify after programming asserted
//          MCLR, causing a VPP voltage fault and the verify to fail.
// Bug Fix: Added a bit of height to the Config Editor window to prevent bottom getting chopped off on some Asian
//          editions of Windows.
// Feature: Programmer-To-Go updated to include support for 3rd party workalike devices with more than 256K of
//          exteranl memory.  The INI parameter PTGM now supports values 0=128K to 5=4MB (with intervening values
//          being powers of 2.)
//
// version 2.61.01 - 24 March 2009 WEK
// Bug Fix: Addresses an issue where the Calibration default voltage of 4.000 Volts uses the incorrect decimal
//          symbol "." for some European systems which use "," instead.

// version 3.00.00 - 19 April 2012 MSHK
// Major changes, add PICkit 3 support.
//
// version 3.00.05 - 31 July 2012 MWR
// Major changes, added HCS and Serial EE support.


namespace PICkit2V2
{
	public delegate void DelegateUpdateGUI();
	public delegate bool DelegateVerify();
	public delegate bool DelegateWrite(bool verify);
	public delegate void DelegateRead();
	public delegate void DelegateErase(bool writeOSCCAL);
	public delegate void DelegateWriteCal(uint[] calwords);
	public delegate bool DelegateBlankCheck();
	public delegate void DelegateMultiProgMemClosed();
	public delegate void DelegateMultiEEMemClosed();
	public delegate void DelegateMemEdited();
	public delegate void DelegateStatusWin(string dispText);
	public delegate void DelegateResetStatusBar(int maxValue);
	public delegate void DelegateStepStatusBar();
	public delegate void DelegateOpenProgToGoGuide();
	public delegate void DelegateVddCallback(bool force, bool forceState);

	public partial class FormPICkit2 : Form
	{
		public static bool ShowWriteEraseVDDDialog = true;
		public static bool ContinueWriteErase = false;
		public static bool setOSCCALValue = false;
		public static bool ConfigsEdited = false;
		public static bool TestMemoryOpen = false;
		public static bool TestMemoryEnabled = false;
		public static int TestMemoryWords = 64;
		public static ushort pk2number = 0;
		public static bool TestMemoryImportExport = false;
		public static FormTestMemory formTestMem;
		public static string DeviceFileName = "PK2DeviceFile.dat";
		[StructLayout(LayoutKind.Sequential)]
		public struct FLASHWINFO
		{
			public UInt16 cbSize;
			public IntPtr hwnd;
			public UInt32 dwFlags;
			public UInt16 uCount;
			public UInt32 dwTimeout;
		}
		public static float ScalefactW = 1F;   // scaling factors for dealing with non-standard DPI
		public static float ScalefactH = 1F;
		public static string HomeDirectory;
		public static byte slowSpeedICSP = 4; // default value
		public static bool PlaySuccessWav = false;
		public static string SuccessWavFile = "\\Sounds\\success.wav";
		public static bool PlayWarningWav = false;
		public static string WarningWavFile = "\\Sounds\\warning.wav";
		public static bool PlayErrorWav = false;
		public static string ErrorWavFile = "\\Sounds\\error.wav";

		//private static int[] familyMenuTable;   // Keeps track of which menu index is which family index
		private static bool selfPoweredTarget;
		private static KONST.StatusColor statusWindowColor = Constants.StatusColor.normal;
		private DialogVDDErase dialogVddErase = new DialogVDDErase();
		private DialogUserIDs dialogIDMemory;
		private KONST.VddTargetSelect VddTargetSave = KONST.VddTargetSelect.auto;
		private DialogUART uartWindow = new DialogUART();
		private DialogLogic logicWindow = new DialogLogic();
		private FormMultiWinProgMem programMemMultiWin = new FormMultiWinProgMem();
		private FormMultiWinEEData eepromDataMultiWin = new FormMultiWinEEData();
		private Point lastLoc = new Point(0, 0);
		private bool buttonLast = true;
		private bool checkImportFile = false;
		private bool oldFirmware = false;
		private bool bootLoad = false;
		private bool importGo = false;
		private bool allowDataEdits = true;
		private bool progMemJustEdited = false;
		private bool eeMemJustEdited = false;
		private bool testConnected = false;
		private bool searchOnStartup = true;
		private bool autoDetectInINI = true;
		private bool selectDeviceFile = false;
		private bool viewChanged = false;
		private bool multiWindow = false;
		private bool multiWinPMemOpen = true;
		private bool multiWinEEMemOpen = true;
		private bool saveMultWinPMemOpen = true;
		private bool saveMultiWinEEMemOpen = true;
		private bool verifyOSCCALValue = true;
		private bool mainWinOwnsMem = true;
		private bool usePE33 = true;
		private bool usePE24 = true;
		private bool useLVP = false;
		private bool deviceVerification = true;
		private byte ptgMemory = 0; // 128K default
		private string lastFamily = "Midrange";
		private string hex1 = "";
		private string hex2 = "";
		private string hex3 = "";
		private string hex4 = "";
		private System.Media.SoundPlayer wavPlayer = new System.Media.SoundPlayer();



		[DllImport("user32.dll")]
		static extern Int16 FlashWindowEx(ref FLASHWINFO pwfi);

		public FormPICkit2()
		{

			InitializeComponent();

			float lastSetVdd = loadINI();  // read INI file.      

			// Add the multiview forms as owned by this form.
			if (mainWinOwnsMem)
			{
				this.AddOwnedForm(programMemMultiWin);
				this.AddOwnedForm(eepromDataMultiWin);
			}

			initializeGUI();

			if (!loadDeviceFile())
			{  // no device file - exit
				return;
			}
			if (toolStripMenuItemManualSelect.Checked)
				ManualAutoSelectToggle(false);

			buildDeviceMenu();

			if (!allowDataEdits)
			{
				dataGridProgramMemory.ReadOnly = true;
				dataGridViewEEPROM.ReadOnly = true;
			}

			// create a valid set of buffers, even if no device found
			Pk2.ResetBuffers();

			//Test connection
			testConnected = checkForTest();
			if (testConnected)
			{
				testConnected = testMenuBuild();
			}

			// assign delegates
			P32.UpdateStatusWinText = new DelegateStatusWin(this.StatusWinWr);
			P32.ResetStatusBar = new DelegateResetStatusBar(this.ResetStatusBar);
			P32.StepStatusBar = new DelegateStepStatusBar(this.StepStatusBar);
			PE33.UpdateStatusWinText = new DelegateStatusWin(this.StatusWinWr);
			PE33.ResetStatusBar = new DelegateResetStatusBar(this.ResetStatusBar);
			PE33.StepStatusBar = new DelegateStepStatusBar(this.StepStatusBar);
			PE24.UpdateStatusWinText = new DelegateStatusWin(this.StatusWinWr);
			PE24.ResetStatusBar = new DelegateResetStatusBar(this.ResetStatusBar);
			PE24.StepStatusBar = new DelegateStepStatusBar(this.StepStatusBar);
			Pk3h.ResetStatusBar = new DelegateResetStatusBar(this.ResetStatusBar);
			Pk3h.StepStatusBar = new DelegateStepStatusBar(this.StepStatusBar);

			uartWindow.VddCallback = new DelegateVddCallback(this.SetVddState);
			logicWindow.VddCallback = new DelegateVddCallback(this.SetVddState);

			if (!detectPICkit2(KONST.ShowMessage, true))
			{ // if we don't find it, wait a bit and give it another chance.
				if (bootLoad)
					return;
				if (!oldFirmware)
				{
					Thread.Sleep(3000);
					if (!detectPICkit2(KONST.ShowMessage, true))
					{
						return;
					}
				}
				else
				{
					TestMemoryOpen = false;
					timerDLFW.Enabled = true;
					return;
				}
			}

			partialEnableGUIControls();

			if (!Pk2.isPK3)          // TODO: PICkit 2 only for now
				Pk2.ExitUARTMode(); // Just in case.
			Pk2.VddOff();
			Pk2.SetVDDVoltage(3.3F, 0.85F);

			if (autoDetectToolStripMenuItem.Checked)
			{
				lookForPoweredTarget(KONST.NoMessage);
			}

			if (searchOnStartup && Pk2.DetectDevice(KONST.SEARCH_ALL_FAMILIES, true, chkBoxVddOn.Checked))
			{
				setGUIVoltageLimits(true);
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);
				displayStatusWindow.Text = displayStatusWindow.Text + "\nPIC Device Found.";
				fullEnableGUIControls();
			}
			else
			{
				for (int i = 0; i < Pk2.DevFile.Info.NumberFamilies; i++)
				{
					if (Pk2.DevFile.Families[i].FamilyName == lastFamily)
					{
						Pk2.SetActiveFamily(i);
						if (!Pk2.DevFile.Families[i].PartDetect)
						{ // families with listed parts
							buildDeviceSelectDropDown(i);
							comboBoxSelectPart.Visible = true;
							comboBoxSelectPart.SelectedIndex = 0;
							displayDevice.Visible = true;
						}
					}
				}
				// Set unsupported part to voltages of first part in the selected family
				for (int p = 1; p < Pk2.DevFile.Info.NumberParts; p++)
				{ // start at 1 so don't search Unsupported Part
					if (Pk2.DevFile.PartsList[p].Family == Pk2.GetActiveFamily())
					{
						Pk2.DevFile.PartsList[0].VddMax = Pk2.DevFile.PartsList[p].VddMax;
						Pk2.DevFile.PartsList[0].VddMin = Pk2.DevFile.PartsList[p].VddMin;
						break;
					}
				}
				setGUIVoltageLimits(true);
			}

			if ((lastSetVdd != 0F) && (Pk2.DevFile.Families[Pk2.GetActiveFamily()].FamilyName == lastFamily)
				&& !selfPoweredTarget)
			{ // if there was a saved VDD and the part family is the same
				if (lastSetVdd > (float)numUpDnVDD.Maximum)
				{
					lastSetVdd = (float)numUpDnVDD.Maximum;
				}
				if (lastSetVdd < (float)numUpDnVDD.Minimum)
				{
					lastSetVdd = (float)numUpDnVDD.Minimum;
				}
				numUpDnVDD.Value = (decimal)lastSetVdd;
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);
			}

			checkForPowerErrors();

			if (TestMemoryEnabled)
			{
				toolStripMenuItemTestMemory.Visible = true;
				if (TestMemoryOpen)
				{
					openTestMemory();
				}
			}

			if (!Pk2.DevFile.Families[Pk2.GetActiveFamily()].PartDetect)
			{  // if drop-down select family, fully disable GUI
				disableGUIControls();
			}

			if (multiWindow)
			{
				saveMultWinPMemOpen = multiWinPMemOpen;
				toolStripMenuItemShowProgramMemory.Checked = false;
				multiWinPMemOpen = false;
				saveMultiWinEEMemOpen = multiWinEEMemOpen;
				toolStripMenuItemShowEEPROMData.Checked = false;
				multiWinEEMemOpen = false;
			}

			updateGUI(KONST.UpdateMemoryDisplays);

			if (multiWindow)
				timerInitalUpdate.Enabled = true;
		}

		public void ExtCallUpdateGUI()
		{
			updateGUI(KONST.UpdateMemoryDisplays);
		}

		public bool ExtCallVerify()
		{
			return deviceVerify(false, 0, false);
		}

		public bool ExtCallWrite(bool verify)
		{
			bool menuSave = verifyOnWriteToolStripMenuItem.Checked;
			if (verify)
			{
				verifyOnWriteToolStripMenuItem.Checked = true;
			}
			else
			{
				verifyOnWriteToolStripMenuItem.Checked = false;
			}

			bool result = deviceWrite();

			verifyOnWriteToolStripMenuItem.Checked = menuSave;

			return result;
		}

		public void ExtCallRead()
		{
			deviceRead();
		}

		public void ExtCallErase(bool writeOSCCAL)
		{
			eraseDeviceAll(writeOSCCAL, new uint[0]);
		}

		public void ExtCallCalEraseWrite(uint[] calwords)
		{
			eraseDeviceAll(false, calwords);
		}

		public bool ExtCallBlank()
		{
			return blankCheckDevice();
		}

		public void MultiWinProgMemClosed()
		{
			multiWinPMemOpen = false;
			toolStripMenuItemShowProgramMemory.Checked = false;
		}

		public void MultiWinEEMemClosed()
		{
			multiWinEEMemOpen = false;
			toolStripMenuItemShowEEPROMData.Checked = false;
		}

		public void ShowMemEdited()
		{
			displayDataSource.Text = "Edited.";
			checkImportFile = false;
		}

		public void StatusWinWr(string dispText)
		{
			displayStatusWindow.Text = dispText;
			this.Update();
		}

		public void ResetStatusBar(int maxValue)
		{
			progressBar1.Value = 0;
			progressBar1.Maximum = maxValue;
			this.Update();
		}

		public void StepStatusBar()
		{
			progressBar1.PerformStep();
		}

		public void SetVddState(bool force, bool forceState)
		{
			vddControl(force, forceState);
			uartWindow.SetVddBox(numUpDnVDD.Enabled, chkBoxVddOn.Checked);
			logicWindow.SetVddBox(numUpDnVDD.Enabled, chkBoxVddOn.Checked);
		}

		// ===================================== PRIVATE METHODS ===================================================        
		private bool checkForPowerErrors()
		{
			Thread.Sleep(100);      // sleep a bit to allow time for error to develop.

			KONST.PICkit2PWR result = Pk2.PowerStatus();
			if (result == KONST.PICkit2PWR.vdderror)
			{
				if (!timerAutoImportWrite.Enabled)
				{ // don't show in AutoImportWrite mode
					MessageBox.Show(Pk2.ToolName + " VDD voltage level error.\nCheck target & retry operation.", Pk2.ToolName + " Error");
				}
			}
			else if (result == KONST.PICkit2PWR.vpperror)
			{
				if (!timerAutoImportWrite.Enabled)
				{ // don't show in AutoImportWrite mode
					MessageBox.Show(Pk2.ToolName + " VPP voltage level error.\nCheck target & retry operation.", Pk2.ToolName + " Error");
				}
			}
			else if (result == KONST.PICkit2PWR.vddvpperrors)
			{
				if (!timerAutoImportWrite.Enabled)
				{ // don't show in AutoImportWrite mode
					MessageBox.Show(Pk2.ToolName + " VDD and VPP voltage level errors.\nCheck target & retry operation.", Pk2.ToolName + " Error");
				}
			}
			else if (result == KONST.PICkit2PWR.vdd_on)
			{
				chkBoxVddOn.Checked = true;
				return false;  // no error           
			}
			else if (result == KONST.PICkit2PWR.vdd_off)
			{
				chkBoxVddOn.Checked = false;
				return false; // no error
			}

			chkBoxVddOn.Checked = false;
			return true;    // error
		}

		private bool lookForPoweredTarget(bool showMessageBox)
		{
			float vdd = 0;
			float vpp = 0;

			if (fastProgrammingToolStripMenuItem.Checked)
			{
				Pk2.SetProgrammingSpeed(0);
			}
			else
			{
				Pk2.SetProgrammingSpeed(slowSpeedICSP);
			}

			if (autoDetectToolStripMenuItem.Checked)
			{
				if (Pk2.CheckTargetPower(ref vdd, ref vpp) == KONST.PICkit2PWR.selfpowered)
				// self powered target found
				{
					chkBoxVddOn.Checked = false;
					if (!selfPoweredTarget)
					{ // Only execute if present Vdd source is PICkit 2
						selfPoweredTarget = true;
						chkBoxVddOn.Enabled = true;
						chkBoxVddOn.Text = "Check";
						numUpDnVDD.Enabled = false;
						groupBoxVDD.Text = "VDD Target";
						if (showMessageBox)
						{
							MessageBox.Show("Powered target detected.\n VDD source set to target.", "Target VDD");
						}
					}
					// update VDD value each time, though
					numUpDnVDD.Maximum = (decimal)vdd;
					numUpDnVDD.Value = (decimal)vdd;
					numUpDnVDD.Update();
					return true;
				}
				else
				{
					if (selfPoweredTarget)
					{ // Only execute if present Vdd source is target
						selfPoweredTarget = false;
						chkBoxVddOn.Enabled = true;
						chkBoxVddOn.Text = "On";
						numUpDnVDD.Enabled = true;
						setGUIVoltageLimits(true);
						groupBoxVDD.Text = "VDD " + Pk2.ToolName;
						if (showMessageBox)
						{
							MessageBox.Show("Unpowered target detected.\n VDD source set to " + Pk2.ToolName + ".", "Target VDD");
						}
					}
					return false;
				}
			}
			else if (forcePICkit2ToolStripMenuItem.Checked)
			{
				if (selfPoweredTarget)
				{ // Only execute if present Vdd source is target
					Pk2.ForcePICkitPowered();
					selfPoweredTarget = false;
					chkBoxVddOn.Enabled = true;
					chkBoxVddOn.Text = "On";
					numUpDnVDD.Enabled = true;
					setGUIVoltageLimits(true);
					groupBoxVDD.Text = "VDD " + Pk2.ToolName;
				}
				return false;
			}
			else //forceTargetToolStripMenuItem
			{
				// read target voltage
				Pk2.CheckTargetPower(ref vdd, ref vpp); // if target no connected, will see low VDD so set unpowered
				Pk2.ForceTargetPowered();               // therefore, we must force it here.   
				chkBoxVddOn.Checked = false;
				if (!selfPoweredTarget)
				{ // Only execute if present Vdd source is PICkit 2
					selfPoweredTarget = true;
					chkBoxVddOn.Enabled = true;
					chkBoxVddOn.Text = "Check";
					numUpDnVDD.Enabled = false;
					groupBoxVDD.Text = "VDD Target";
				}
				// update VDD value each time, though
				numUpDnVDD.Maximum = (decimal)vdd;
				numUpDnVDD.Value = (decimal)vdd;
				numUpDnVDD.Update();
				return true;
			}
		}

		private void setGUIVoltageLimits(bool setValue)
		{
			if (numUpDnVDD.Enabled)
			{ // don't set limits if self-powered
				numUpDnVDD.Maximum = (decimal)Pk2.DevFile.PartsList[Pk2.ActivePart].VddMax;
				numUpDnVDD.Minimum = (decimal)Pk2.DevFile.PartsList[Pk2.ActivePart].VddMin;
				// set unsupported part to current family Vdd Max/Min
				if (Pk2.ActivePart != 0)
				{// don't set if current part is the unsupported!
					Pk2.DevFile.PartsList[0].VddMax = Pk2.DevFile.PartsList[Pk2.ActivePart].VddMax;
					Pk2.DevFile.PartsList[0].VddMin = Pk2.DevFile.PartsList[Pk2.ActivePart].VddMin;
				}
				if (setValue)
				{
					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].VddMax <= 4.0)
						&& (Pk2.DevFile.PartsList[Pk2.ActivePart].VddMax >= 3.3))
					{
						numUpDnVDD.Value = 3.3M; // set to 3.3 V nominal
					}
					else if (Pk2.DevFile.PartsList[Pk2.ActivePart].VddMax == 5.0)
					{
						numUpDnVDD.Value = 5.0M; // set to 5.0 V nominal
					}
					else
					{
						numUpDnVDD.Value = (decimal)Pk2.DevFile.PartsList[Pk2.ActivePart].VddMax;
					}
				}
			}
		}

		private void initializeGUI()
		{
			// set scale factors
			ScalefactW = ((float)dataGridProgramMemory.Size.Width) / 502F;
			ScalefactH = ((float)dataGridProgramMemory.Size.Height) / 208F;

			// Init the Config word data grid
			dataGridConfigWords.BackgroundColor = System.Drawing.SystemColors.Control;  // same color as cells so don't
			// notice misaligns at other DPIs
			dataGridConfigWords.ColumnCount = KONST.ConfigColumns;  // 2x8 grid        
			dataGridConfigWords.RowCount = KONST.ConfigRows;
			dataGridConfigWords.DefaultCellStyle.BackColor
					= System.Drawing.SystemColors.Control;          // set color
			dataGridConfigWords[0, 0].Selected = true;              // these 2 statements remove the "select" box
			dataGridConfigWords[0, 0].Selected = false;
			int adjspacing = (int)(40 * ScalefactW);
			for (int column = 0; column < KONST.ConfigColumns; column++)
			{
				//dataGridConfigWords.Columns[column].Width = 40;                    
				dataGridConfigWords.Columns[column].Width = adjspacing;
			}
			//dataGridConfigWords.Rows[0].Height = 17;
			//dataGridConfigWords.Rows[1].Height = 17;
			dataGridConfigWords.Rows[0].Height = (int)(17 * ScalefactH);
			dataGridConfigWords.Rows[1].Height = (int)(17 * ScalefactH);

			// Init progress bar.
			progressBar1.Step = 1;

			// Init the Program Memory grid.
			if (comboBoxProgMemView.SelectedIndex < 0)
			{
				comboBoxProgMemView.SelectedIndex = 0;
			}
			// Set default Cell font
			dataGridProgramMemory.DefaultCellStyle.Font = new Font("Courier New", 9);
			dataGridProgramMemory.ColumnCount = 9;
			dataGridProgramMemory.RowCount = 512;
			dataGridProgramMemory[0, 0].Selected = true;              // these 2 statements remove the "select" box
			dataGridProgramMemory[0, 0].Selected = false;
			adjspacing = (int)(59 * ScalefactW);
			//dataGridProgramMemory.Columns[0].Width = 59; // address column
			dataGridProgramMemory.Columns[0].Width = adjspacing; // address column
			dataGridProgramMemory.Columns[0].ReadOnly = true;
			adjspacing = (int)(53 * ScalefactW);
			for (int column = 1; column < 9; column++)
			{
				//dataGridProgramMemory.Columns[column].Width = 53; // data columns
				dataGridProgramMemory.Columns[column].Width = adjspacing; // data columns
			}
			for (int row = 0; row < 32; row++)
			{
				dataGridProgramMemory[0, row].Style.BackColor = System.Drawing.SystemColors.ControlLight;
				dataGridProgramMemory[0, row].Value = string.Format("{0:X5}", row * 8);
			}

			// Init the EEPROM grid.
			if (comboBoxEE.SelectedIndex < 0)
			{
				comboBoxEE.SelectedIndex = 0;
			}
			// Set default Cell font
			dataGridViewEEPROM.DefaultCellStyle.Font = new Font("Courier New", 9);
			dataGridViewEEPROM.ColumnCount = 9;
			dataGridViewEEPROM.RowCount = 16;
			adjspacing = (int)(40 * ScalefactW);
			//dataGridViewEEPROM.Columns[0].Width = 40; // address column
			dataGridViewEEPROM.Columns[0].Width = adjspacing; // address column
			dataGridViewEEPROM.Columns[0].ReadOnly = true;
			adjspacing = (int)(41 * ScalefactW);
			for (int column = 1; column < 9; column++)
			{
				//dataGridViewEEPROM.Columns[column].Width = 41; // data columns
				dataGridViewEEPROM.Columns[column].Width = adjspacing; // data columns
			}
			dataGridViewEEPROM[0, 0].Selected = true;              // these 2 statements remove the "select" box
			dataGridViewEEPROM[0, 0].Selected = false;
			dataGridViewEEPROM.Visible = false;

			updateAlertSoundCheck();

			// create delegates
			programMemMultiWin.TellMainFormProgMemClosed = new DelegateMultiProgMemClosed(this.MultiWinProgMemClosed);
			programMemMultiWin.TellMainFormProgMemEdited = new DelegateMemEdited(this.ShowMemEdited);
			programMemMultiWin.TellMainFormUpdateGUI = new DelegateUpdateGUI(this.ExtCallUpdateGUI);
			eepromDataMultiWin.TellMainFormEEMemClosed = new DelegateMultiEEMemClosed(this.MultiWinEEMemClosed);
			eepromDataMultiWin.TellMainFormProgMemEdited = new DelegateMemEdited(this.ShowMemEdited);
			eepromDataMultiWin.TellMainFormUpdateGUI = new DelegateUpdateGUI(this.ExtCallUpdateGUI);

			if (!Pk2.isPK3)
			{
				// Hide the PICkit 3 menu items.
				this.Text = "PICkit 2 Programmer";
				calibrateToolStripMenuItem.Text = "Calibrate VDD & Set Unit ID...";
				revertToMPLABModeToolStripMenuItem.Visible = false;
				usersGuidePk3ToolStripMenuItem.Visible = false;
				webPk3ToolStripMenuItem.Visible = false;
				readMePk3ToolStripMenuItem.Visible = false;
			}
			else
			{
				// Hide the PICkit 2 and unsupported menu items.
				this.Text = "PICkit 3 Programmer";
				troubleshhotToolStripMenuItem.Enabled = false;
				calAutoRegenerateToolStripMenuItem.Visible = false;
				toolStripMenuItemProgToGo.Visible = false;
				toolStripMenuItemLogicToolUG.Visible = false;
				usersGuidePk2ToolStripMenuItem.Visible = false;
				uG44pinToolStripMenuItem.Visible = false;
				lpcUsersGuideToolStripMenuItem.Visible = false;
				webPk2ToolStripMenuItem.Visible = false;
				readMePk2ToolStripMenuItem.Visible = false;
				troubleshhotToolStripMenuItem.Visible = false;

				openFWFile = openFWFilePk3;
			}
			groupBoxVDD.Text = "VDD " + Pk2.ToolName;

		}

		private bool loadDeviceFile()
		{
			// Select device file?
			if (selectDeviceFile)
			{
				DialogDevFile selectDialog = new DialogDevFile();
				selectDialog.ShowDialog();
			}

			if (Pk2.ReadDeviceFile(DeviceFileName))
			{
				if (Pk2.DevFile.Info.Compatibility < KONST.DevFileCompatLevelMin)
				{
					displayStatusWindow.Text =
						"Older device file is not compatible with this " + Pk2.ToolName + "\nversion.  Visit www.microchip.com for updates.";
					checkCommunicationToolStripMenuItem.Enabled = false;
					return false;
				}
				if (Pk2.DevFile.Info.Compatibility > KONST.DevFileCompatLevel)
				{
					displayStatusWindow.Text =
						"The device file requires a newer version of " + Pk2.ToolName + ".\nVisit www.microchip.com for updates.";
					checkCommunicationToolStripMenuItem.Enabled = false;
					return false;
				}
				return true;
			}
			displayStatusWindow.Text =
				"Device file " + DeviceFileName + " not found.\nMust be in same directory as executable.";
			checkCommunicationToolStripMenuItem.Enabled = false;
			return false;
		}


		private bool detectPICkit2(bool showFound, bool detectMult)
		{
			KONST.PICkit2USB detectResult;

			if (detectMult)
			{ // look for PICkit 2's
				pk2number = 0; // default to first device
				detectResult = Pk2.DetectPICkit2Device(0, false);
				if (detectResult != KONST.PICkit2USB.notFound)
				{
					KONST.PICkit2USB detRes = Pk2.DetectPICkit2Device(1, false);
					if (detRes != KONST.PICkit2USB.notFound)
					{
						DialogUnitSelect selectUnitDialog = new DialogUnitSelect();
						selectUnitDialog.ShowDialog();
					}
				}

			}
			// connect to selected (or default) device
			detectResult = Pk2.DetectPICkit2Device(pk2number, true);

			if (detectResult == KONST.PICkit2USB.found)
			{
				downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;

				if (Pk2.isPK3)
				{
					revertToMPLABModeToolStripMenuItem.Enabled = true;
				}

				chkBoxVddOn.Enabled = true;
				if (!selfPoweredTarget)
				{ // don't enable if self-powered.
					numUpDnVDD.Enabled = true;
				}
				deviceToolStripMenuItem.Enabled = true;
				if (showFound)
				{
					//string unitID = Pk2.UnitIDRead();
					string unitID = Pk2.GetSerialUnitID();

					if (unitID[0] == '-')
					{
						displayStatusWindow.Text = Pk2.ToolName + " found and connected.";
						this.Text = "PICkit 2 Programmer";
					}
					else
					{
						displayStatusWindow.Text = Pk2.ToolName + " connected.  ID = " + unitID;
						this.Text = Pk2.ToolName + " Programmer - " + unitID;
					}
				}
				return true;
			}
			else
			{
				downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;
				chkBoxVddOn.Enabled = false;
				numUpDnVDD.Enabled = false;
				deviceToolStripMenuItem.Enabled = false;
				disableGUIControls();
				if (detectResult == KONST.PICkit2USB.firmwareInvalid && !Pk2.isPK3)
				{
					displayStatusWindow.BackColor = Color.Yellow;
					downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;    // need to be able to download!
					displayStatusWindow.Text =
					"The PICkit 2 OS v" + Pk2.FirmwareVersion + " must be updated.\nUse the Tools menu to download a new OS.";

					oldFirmware = true;
					return false;
				}
				if (detectResult == KONST.PICkit2USB.bootloader)
				{
					displayStatusWindow.BackColor = Color.Yellow;
					downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;    // need to be able to download!
					displayStatusWindow.Text =
						"The " + Pk2.ToolName + " has no Operating System.\nUse the Tools menu to download an OS.";
					bootLoad = true;
					return false;
				}
				if (detectResult == KONST.PICkit2USB.pk3mplab)
				{
					displayStatusWindow.BackColor = Color.Yellow;
					downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;    // need to be able to download!
					displayStatusWindow.Text =
						"The PICkit 3 is in MPLAB mode. Use the Tools menu to download an OS compatible with this application.";
					bootLoad = true;
					return false;
				}
				if (detectResult == KONST.PICkit2USB.firmwareInvalid && Pk2.isPK3)
				{
					displayStatusWindow.BackColor = Color.Yellow;
					//downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;    // need to be able to download!
					displayStatusWindow.Text =
					"The PICkit 3 has a corrupt image and must be reprogrammed with an external programmer";
					bootLoad = false;
					return false;
				}
				if (detectResult == KONST.PICkit2USB.firmwareOldversion && Pk2.isPK3)
				{
					displayStatusWindow.BackColor = Color.Yellow;
					downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;    // need to be able to download!
					displayStatusWindow.Text =
					"The PICkit 3 OS v" + Pk2.FirmwareVersion + " must be updated.\nUse the Tools menu to download a new OS.";

					oldFirmware = true;
					return false;
				}
				displayStatusWindow.BackColor = Color.Salmon;
				displayStatusWindow.Text =
					Pk2.ToolName + " not found.  Check USB connections and \nuse Tools->Check Communication to retry.";
				return false;
			}
		}

		private void disableGUIControls()
		{
			importFileToolStripMenuItem.Enabled = false;
			exportFileToolStripMenuItem.Enabled = false;
			//programmerToolStripMenuItem.Enabled = false;
			readDeviceToolStripMenuItem.Enabled = false;
			writeDeviceToolStripMenuItem.Enabled = false;
			verifyToolStripMenuItem.Enabled = false;
			eraseToolStripMenuItem.Enabled = false;
			blankCheckToolStripMenuItem.Enabled = false;
			writeOnPICkitButtonToolStripMenuItem.Enabled = false;
			pICkit2GoToolStripMenuItem.Enabled = false;
			setOSCCALToolStripMenuItem.Enabled = false;
			buttonRead.Enabled = false;
			buttonWrite.Enabled = false;
			buttonVerify.Enabled = false;
			buttonErase.Enabled = false;
			buttonBlankCheck.Enabled = false;
			checkBoxProgMemEnabled.Enabled = false;
			checkBoxProgMemEnabledAlt.Enabled = false;
			comboBoxProgMemView.Enabled = false;
			dataGridProgramMemory.ForeColor = System.Drawing.SystemColors.GrayText;
			dataGridProgramMemory.Enabled = false;
			dataGridViewEEPROM.Visible = false;
			comboBoxEE.Enabled = false;
			checkBoxEEMem.Enabled = false;
			checkBoxEEDATAMemoryEnabledAlt.Enabled = false;
			buttonExportHex.Enabled = false;
			checkBoxAutoImportWrite.Enabled = false;
			troubleshhotToolStripMenuItem.Enabled = false;
			calibrateToolStripMenuItem.Enabled = false;
			programMemMultiWin.DisplayDisable();
			eepromDataMultiWin.DisplayDisable();
			UARTtoolStripMenuItem.Enabled = false;
			toolStripMenuItemLogicTool.Enabled = false;
			if (Pk2.isPK3)
			{
				downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;
				revertToMPLABModeToolStripMenuItem.Enabled = false;
			}

		}

		private void partialEnableGUIControls()
		{
			importFileToolStripMenuItem.Enabled = true;
			exportFileToolStripMenuItem.Enabled = false;
			//programmerToolStripMenuItem.Enabled = true;
			readDeviceToolStripMenuItem.Enabled = true;
			writeDeviceToolStripMenuItem.Enabled = true;
			verifyToolStripMenuItem.Enabled = true;
			eraseToolStripMenuItem.Enabled = true;
			blankCheckToolStripMenuItem.Enabled = true;
			writeOnPICkitButtonToolStripMenuItem.Enabled = true;
			setOSCCALToolStripMenuItem.Enabled = false;
			writeDeviceToolStripMenuItem.Enabled = false;
			verifyToolStripMenuItem.Enabled = false;
			buttonRead.Enabled = true;
			buttonWrite.Enabled = false;
			buttonVerify.Enabled = false;
			buttonErase.Enabled = true;
			buttonBlankCheck.Enabled = true;
			checkBoxProgMemEnabled.Enabled = false;
			checkBoxProgMemEnabledAlt.Enabled = false;
			comboBoxProgMemView.Enabled = false;
			dataGridProgramMemory.ForeColor = System.Drawing.SystemColors.GrayText;
			dataGridProgramMemory.Enabled = false;
			dataGridViewEEPROM.Visible = false;
			comboBoxEE.Enabled = false;
			checkBoxEEMem.Enabled = false;
			checkBoxEEDATAMemoryEnabledAlt.Enabled = false;
			buttonExportHex.Enabled = false;
			checkBoxAutoImportWrite.Enabled = false;
			troubleshhotToolStripMenuItem.Enabled = true;
			calibrateToolStripMenuItem.Enabled = true;
			programMemMultiWin.DisplayDisable();
			eepromDataMultiWin.DisplayDisable();
			toolStripMenuItemLogicTool.Enabled = true;

			// currently not supported in the PICkit 3 GUI
			if (!Pk2.isPK3)
			{
				pICkit2GoToolStripMenuItem.Enabled = true;
				UARTtoolStripMenuItem.Enabled = true;
			}
		}

		private void semiEnableGUIControls()
		{
			importFileToolStripMenuItem.Enabled = true;
			exportFileToolStripMenuItem.Enabled = false;
			//programmerToolStripMenuItem.Enabled = true;
			readDeviceToolStripMenuItem.Enabled = true;
			writeDeviceToolStripMenuItem.Enabled = true;
			verifyToolStripMenuItem.Enabled = true;
			eraseToolStripMenuItem.Enabled = true;
			blankCheckToolStripMenuItem.Enabled = true;
			writeOnPICkitButtonToolStripMenuItem.Enabled = true;
			writeDeviceToolStripMenuItem.Enabled = true;
			verifyToolStripMenuItem.Enabled = true;
			setOSCCALToolStripMenuItem.Enabled = false;
			buttonRead.Enabled = true;
			buttonWrite.Enabled = true;
			buttonVerify.Enabled = true;
			buttonErase.Enabled = true;
			buttonBlankCheck.Enabled = true;
			checkBoxProgMemEnabled.Enabled = false;
			checkBoxProgMemEnabledAlt.Enabled = false;
			comboBoxProgMemView.Enabled = false;
			dataGridProgramMemory.ForeColor = System.Drawing.SystemColors.GrayText;
			dataGridProgramMemory.Enabled = false;
			dataGridViewEEPROM.Visible = true;
			dataGridViewEEPROM.ForeColor = System.Drawing.SystemColors.GrayText;
			comboBoxEE.Enabled = false;
			checkBoxEEMem.Enabled = false;
			checkBoxEEDATAMemoryEnabledAlt.Enabled = false;
			buttonExportHex.Enabled = false;
			checkBoxAutoImportWrite.Enabled = true;
			troubleshhotToolStripMenuItem.Enabled = true;
			calibrateToolStripMenuItem.Enabled = true;
			toolStripMenuItemLogicTool.Enabled = true;

			// currently not supported in the PICkit 3 GUI
			if (!Pk2.isPK3)
			{
				pICkit2GoToolStripMenuItem.Enabled = true;
				UARTtoolStripMenuItem.Enabled = true;
			}
		}

		private void fullEnableGUIControls()
		{
			importFileToolStripMenuItem.Enabled = true;
			exportFileToolStripMenuItem.Enabled = true;
			//programmerToolStripMenuItem.Enabled = true;
			readDeviceToolStripMenuItem.Enabled = true;
			writeDeviceToolStripMenuItem.Enabled = true;
			verifyToolStripMenuItem.Enabled = true;
			eraseToolStripMenuItem.Enabled = true;
			blankCheckToolStripMenuItem.Enabled = true;
			writeOnPICkitButtonToolStripMenuItem.Enabled = true;
			writeDeviceToolStripMenuItem.Enabled = true;
			verifyToolStripMenuItem.Enabled = true;
			buttonRead.Enabled = true;
			buttonWrite.Enabled = true;
			buttonVerify.Enabled = true;
			buttonErase.Enabled = true;
			buttonBlankCheck.Enabled = true;
			checkBoxProgMemEnabled.Enabled = true;
			checkBoxProgMemEnabledAlt.Enabled = true;
			comboBoxProgMemView.Enabled = true;
			dataGridProgramMemory.Enabled = true;
			dataGridProgramMemory.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewEEPROM.ForeColor = System.Drawing.SystemColors.WindowText;
			buttonExportHex.Enabled = true;
			checkBoxAutoImportWrite.Enabled = true;
			troubleshhotToolStripMenuItem.Enabled = true;
			calibrateToolStripMenuItem.Enabled = true;
			programMemMultiWin.DisplayEnable();
			eepromDataMultiWin.DisplayEnable();
			toolStripMenuItemLogicTool.Enabled = true;

			// currently not supported in the PICkit 3 GUI
			if (!Pk2.isPK3)
			{
				pICkit2GoToolStripMenuItem.Enabled = true;
				UARTtoolStripMenuItem.Enabled = true;
			}
		}

		private void updateGUIView()
		{
			if (multiWindow)
			{// multiple window view
				// main window
				toolStripMenuItemSingleWindow.Checked = false;
				toolStripMenuItemMultiWindow.Checked = true;
				groupBoxProgMem.Location = new Point((int)(12 * ScalefactW), (int)(300 * ScalefactH));   // move down
				this.Size = new Size((int)(544 * ScalefactW), (int)(320 * ScalefactH));                 // shorten form
				pictureBox1.Location = new Point((int)(423 * ScalefactW), (int)(238 * ScalefactH));      // move up
				buttonExportHex.Location = new Point((int)(311 * ScalefactW), (int)(240 * ScalefactH));  // move over & up
				checkBoxAutoImportWrite.Location = new Point((int)(201 * ScalefactW), (int)(240 * ScalefactH));   // move over & up
				checkBoxProgMemEnabledAlt.Visible = true;       // make visible
				checkBoxEEDATAMemoryEnabledAlt.Visible = true;
				toolStripMenuItemShowProgramMemory.Enabled = true;
				toolStripMenuItemShowEEPROMData.Enabled = true;
				mainWindowAlwaysInFrontToolStripMenuItem.Enabled = true;

				if (mainWinOwnsMem)
					mainWindowAlwaysInFrontToolStripMenuItem.Checked = true;

				// check for windows @ default location.  If so, place them under main window
				Point defLoc = new Point(0, 0);
				if ((programMemMultiWin.Location == defLoc) && (eepromDataMultiWin.Location == defLoc))
				{
					programMemMultiWin.Location = new Point(this.Location.X, this.Location.Y + (int)(321 * ScalefactH));
					eepromDataMultiWin.Location = new Point(this.Location.X, this.Location.Y + (int)(530 * ScalefactH));
				}

				// daughter windows
				if (multiWinPMemOpen)
				{
					programMemMultiWin.Show();
					toolStripMenuItemShowProgramMemory.Checked = true;
				}
				if (multiWinEEMemOpen)
				{
					toolStripMenuItemShowEEPROMData.Checked = true;
					if (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0)
					{
						toolStripMenuItemShowEEPROMData.Enabled = true;
						eepromDataMultiWin.Show();
					}
					else
					{
						toolStripMenuItemShowEEPROMData.Enabled = false;
					}
				}
			}
			else
			{// single window (classic)
				// daughter windows
				programMemMultiWin.Hide();
				eepromDataMultiWin.Hide();

				// main window
				toolStripMenuItemSingleWindow.Checked = true;
				toolStripMenuItemMultiWindow.Checked = false;
				groupBoxProgMem.Location = new Point((int)(12 * ScalefactW), (int)(236 * ScalefactH));  // move back up
				this.Size = new Size((int)(544 * ScalefactW), (int)(670 * ScalefactH));                 // length form back
				pictureBox1.Location = new Point((int)(423 * ScalefactW), (int)(586 * ScalefactH));     // move back down
				buttonExportHex.Location = new Point((int)(423 * ScalefactW), (int)(545 * ScalefactH));  // move back over & down
				checkBoxAutoImportWrite.Location = new Point((int)(423 * ScalefactW), (int)(505 * ScalefactH));   // move back over & down
				checkBoxProgMemEnabledAlt.Visible = false;   // hide
				checkBoxEEDATAMemoryEnabledAlt.Visible = false;
				toolStripMenuItemShowProgramMemory.Enabled = false;
				toolStripMenuItemShowEEPROMData.Enabled = false;
				mainWindowAlwaysInFrontToolStripMenuItem.Enabled = false;
				toolStripMenuItemShowProgramMemory.Checked = false;
				toolStripMenuItemShowEEPROMData.Checked = false;
				mainWindowAlwaysInFrontToolStripMenuItem.Checked = false;
			}

			this.Focus();
		}

		private void updateGUI(bool updateMemories)
		{
			// Check for veiw changed
			if (viewChanged)
			{
				updateGUIView();
				viewChanged = false;
			}

			// update family name
			statusGroupBox.Text = Pk2.DevFile.Families[Pk2.GetActiveFamily()].FamilyName + " Configuration";

			// Update menu if family supports VPP First Program Entry
			if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgEntryVPPScript > 0)
			{
				VppFirstToolStripMenuItem.Enabled = true;
			}
			else
			{
				VppFirstToolStripMenuItem.Checked = false;
				VppFirstToolStripMenuItem.Enabled = false;
			}

			// Update menu for LVP program entry
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].LVPScript > 0)
			{
				string scriptname = Pk2.DevFile.Scripts[Pk2.DevFile.PartsList[Pk2.ActivePart].LVPScript - 1].ScriptName;
				scriptname = scriptname.Substring(scriptname.Length - 2);
				if (scriptname == "HV")
				{
					toolStripMenuItemLVPEnabled.Text = "Use &High Voltage Program Entry";
					labelLVP.Text = "HVP";
				}
				else
				{
					toolStripMenuItemLVPEnabled.Text = "Use &LVP Program Entry";
					labelLVP.Text = "LVP";
				}
				toolStripMenuItemLVPEnabled.Enabled = true;
				if (toolStripMenuItemLVPEnabled.Checked)
					labelLVP.Visible = true;
				else
					labelLVP.Visible = false;
			}
			else
			{
				toolStripMenuItemLVPEnabled.Text = "Use &LVP Program Entry";
				toolStripMenuItemLVPEnabled.Checked = false;
				toolStripMenuItemLVPEnabled.Enabled = false;
				labelLVP.Text = "LVP";
				labelLVP.Visible = false;
			}

			// Update menu for serial EEPROM BIN import/export, and GUI for config display
			if (Pk2.FamilyIsEEPROM())
			{
				importFileToolStripMenuItem.Text = "&Import Hex/Bin";
				exportFileToolStripMenuItem.Text = "&Export Hex/Bin";
				toolStripMenuItemDisplayUnimplConfigAs.Enabled = false;
			}
			else
			{
				importFileToolStripMenuItem.Text = "&Import Hex";
				exportFileToolStripMenuItem.Text = "&Export Hex";
				toolStripMenuItemDisplayUnimplConfigAs.Enabled = true;
			}

			// update device name
			displayDevice.Text = Pk2.DevFile.PartsList[Pk2.ActivePart].PartName;
			if (Pk2.ActivePart == 0)
			{
				if (Pk2.LastDeviceID == 0)
				{
					displayDevice.Text = "No Device Found";
				}
				else
				{
					displayDevice.Text += " (ID=" + string.Format("{0:X4}", Pk2.LastDeviceID) + ")";
				}
			}
			displayDevice.Update();

			// update rev
			displayRev.Text = " <" + string.Format("{0:X2}", Pk2.LastDeviceRev) + ">";

			if (updateMemories)
			{
				// update User IDs
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords > 0)
				{
					labelUserIDs.Enabled = true;

					if (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords < 9)
					{
						displayUserIDs.Visible = true;
						buttonShowIDMem.Visible = false;
						// display the lower byte of each entry
						string userIDLine = "";
						for (int i = 0; i < Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords; i++)
						{
							userIDLine += string.Format("{0:X2} ", (0xFF & Pk2.DeviceBuffers.UserIDs[i]));
						}
						displayUserIDs.Text = userIDLine;
					}
					else
					{ //dsPIC30 unit ID
						displayUserIDs.Visible = false;
						buttonShowIDMem.Visible = true;
						if (DialogUserIDs.IDMemOpen)
						{
							dialogIDMemory.UpdateIDMemoryGrid();
						}
					}
				}
				else
				{
					labelUserIDs.Enabled = false;
					displayUserIDs.Text = "";
					displayUserIDs.Visible = false;
					buttonShowIDMem.Visible = false;

				}
			}
			if (checkBoxProgMemEnabled.Checked)
			{ // Indicate UserIDs not active when Program Memory not selected
				displayUserIDs.ForeColor = System.Drawing.SystemColors.WindowText;
			}
			else
			{
				displayUserIDs.ForeColor = System.Drawing.SystemColors.GrayText;
			}

			// checksum value
			if (updateMemories)
			{
				displayChecksum.Text = string.Format("{0:X4}", Pk2.ComputeChecksum(enableCodeProtectToolStripMenuItem.Checked, enableDataProtectStripMenuItem.Checked));
			}

			// update configuration display
			if (updateMemories)
			{
				if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords == 0) || (Pk2.ActivePart == 0) || !allowDataEdits)
				{
					labelConfig.Enabled = false;
				}
				else
				{
					labelConfig.Enabled = true;
				}

				int configIndex = 0;
				for (int row = 0; row < KONST.ConfigRows; row++)
				{
					for (int column = 0; column < KONST.ConfigColumns; column++)
					{
						if (configIndex < Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords)
						{
							uint configWord = Pk2.DeviceBuffers.ConfigWords[configIndex];
							// how to display unimplemented bits?
							if (as0BitValueToolStripMenuItem.Checked) // as '0'
								configWord &= (uint)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[configIndex];
							else if (as1BitValueToolStripMenuItem.Checked)
								configWord |= ~(uint)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[configIndex];
							// else display as-is
							// But kill off high bits.
							configWord &= (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue & 0xFFFF);
							if (Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1 == configIndex)
							{
								if (enableCodeProtectToolStripMenuItem.Checked)
								{
									if ((Pk2.DeviceBuffers.ConfigWords[Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1] &
										Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask) == Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask)
									{ // no CPs asserted in file
										configWord &= (uint)~Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask;
									}
								}
								if (enableDataProtectStripMenuItem.Checked)
								{
									if ((Pk2.DeviceBuffers.ConfigWords[Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1] &
										 Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask) == Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask)
									{ // no DPs asserted in file
										configWord &= (uint)~Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask;
									}
								}
							}
							dataGridConfigWords[column, row].Value = string.Format("{0:X4}", configWord);
							configIndex++;
						}
						else
						{
							dataGridConfigWords[column, row].Value = " ";
						}
						if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords == 9)
						{
							uint configWord = Pk2.DeviceBuffers.ConfigWords[8];
							// how to display unimplemented bits?
							if (as0BitValueToolStripMenuItem.Checked) // as '0'
								configWord &= (uint)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[8];
							else if (as1BitValueToolStripMenuItem.Checked)
								configWord |= ~(uint)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[8];
							// else display as-is
							// But kill off high bits.
							configWord &= (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue & 0xFFFF);
							labelConfig9.Text = string.Format("{0:X4}", configWord);
							labelConfig9.Visible = true;
						}
						else
						{
							labelConfig9.Visible = false;
						}
					}
				}
			}
			if (checkBoxProgMemEnabled.Checked)
			{ // Indicate Config Words not active when Program Memory not selected
				dataGridConfigWords.ForeColor = System.Drawing.SystemColors.WindowText;
			}
			else
			{
				dataGridConfigWords.ForeColor = System.Drawing.SystemColors.GrayText;
			}

			// update I2C Serial EEPROM Chip Selects
			if (Pk2.FamilyIsEEPROM()
				&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS))
			{
				checkBoxA0CS.Visible = true;
				checkBoxA1CS.Visible = true;
				checkBoxA2CS.Visible = true;
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.CS_PINS_CFG] == 1)
				{
					checkBoxA0CS.Enabled = true;
					checkBoxA1CS.Enabled = false;
					checkBoxA1CS.Checked = false;
					checkBoxA2CS.Enabled = false;
					checkBoxA2CS.Checked = false;
				}
				else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.CS_PINS_CFG] == 2)
				{
					checkBoxA0CS.Enabled = true;
					checkBoxA1CS.Enabled = true;
					checkBoxA2CS.Enabled = false;
					checkBoxA2CS.Checked = false;
				}
				else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.CS_PINS_CFG] == 3)
				{
					checkBoxA0CS.Enabled = true;
					checkBoxA1CS.Enabled = true;
					checkBoxA2CS.Enabled = true;
				}
				else
				{
					checkBoxA0CS.Enabled = false;
					checkBoxA0CS.Checked = false;
					checkBoxA1CS.Enabled = false;
					checkBoxA1CS.Checked = false;
					checkBoxA2CS.Enabled = false;
					checkBoxA2CS.Checked = false;
				}
			}
			else
			{
				checkBoxA0CS.Visible = false;
				checkBoxA1CS.Visible = false;
				checkBoxA2CS.Visible = false;
			}

			// Update OSCCAL
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
			{
				setOSCCALToolStripMenuItem.Enabled = true;
				labelOSCCAL.Enabled = true;
				displayOSCCAL.Text = string.Format("{0:X4}", Pk2.DeviceBuffers.OSCCAL);
				// check for valid OSCCAL value
				if (Pk2.ValidateOSSCAL())
				{
					labelOSSCALInvalid.Visible = false;
					displayOSCCAL.ForeColor = SystemColors.ControlText;
				}
				else
				{
					labelOSSCALInvalid.Visible = true;
					displayOSCCAL.ForeColor = Color.Red;
				}
			}
			else
			{
				labelOSSCALInvalid.Visible = false;
				setOSCCALToolStripMenuItem.Enabled = false;
				labelOSCCAL.Enabled = false;
				displayOSCCAL.Text = "";
			}

			// Update BandGap
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
			{
				labelBandGap.Enabled = true;
				if (Pk2.DeviceBuffers.BandGap == Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue)
				{
					displayBandGap.Text = "";
				}
				else
				{
					displayBandGap.Text = string.Format("{0:X4}", Pk2.DeviceBuffers.BandGap);
				}
			}
			else
			{
				labelBandGap.Enabled = false;
				displayBandGap.Text = "";
			}

			// Status Window Color
			switch (statusWindowColor)
			{
				case Constants.StatusColor.green:
					displayStatusWindow.BackColor = Color.LimeGreen;
					if (PlaySuccessWav)
					{
						wavPlayer.SoundLocation = @SuccessWavFile;
						wavPlayer.Play();
					}
					break;
				case Constants.StatusColor.yellow:
					displayStatusWindow.BackColor = Color.Yellow;
					if (PlayWarningWav)
					{
						wavPlayer.SoundLocation = WarningWavFile;
						wavPlayer.Play();
					}
					break;
				case Constants.StatusColor.red:
					displayStatusWindow.BackColor = Color.Salmon;
					if (PlayErrorWav)
					{
						wavPlayer.SoundLocation = ErrorWavFile;
						wavPlayer.Play();
					}
					break;
				default:
					displayStatusWindow.BackColor = System.Drawing.SystemColors.Info;
					break;
			}
			statusWindowColor = Constants.StatusColor.normal;

			// MCLR checkbox
			if (Pk2.FamilyIsEEPROM())
			{
				checkBoxMCLR.Checked = false;
				checkBoxMCLR.Enabled = false;
				MCLRtoolStripMenuItem.Checked = false;
				MCLRtoolStripMenuItem.Enabled = false;
				Pk2.HoldMCLR(false);
			}
			else
			{
				checkBoxMCLR.Enabled = true;
				MCLRtoolStripMenuItem.Enabled = true;
			}

			// PIC32 doesn't use "Fast Programming" menu item
			if (Pk2.FamilyIsPIC32())
			{
				fastProgrammingToolStripMenuItem.Checked = true;
				fastProgrammingToolStripMenuItem.Enabled = false;
			}
			else
			{
				fastProgrammingToolStripMenuItem.Enabled = true;
			}

			// Update Program Memory display
			if (updateMemories)
			{   // update CP menu
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask == 0)
				{ // no CP
					enableCodeProtectToolStripMenuItem.Checked = false;
					enableCodeProtectToolStripMenuItem.Enabled = false;
				}
				else
				{
					enableCodeProtectToolStripMenuItem.Enabled = true;
				}
			}
			if (updateMemories && multiWindow)
			{
				// init multi window
				if (!programMemMultiWin.InitDone)
					programMemMultiWin.InitProgMemDisplay(comboBoxProgMemView.SelectedIndex);
				// update
				programMemMultiWin.UpdateMultiWinProgMem(displayDataSource.Text);

			}
			if (updateMemories && !multiWindow)
			{
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32 - no ASCII view supported
					comboBoxProgMemView.SelectedIndex = 0;
					comboBoxProgMemView.Enabled = false;
				}
				else
				{
					comboBoxProgMemView.Enabled = true;
				}
				//      Set address columns first       
				//      Set # columns based on blank memory size.
				int dataColumns, columnWidth, columnCount;
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue <= 0xFFF)
				{
					if (Pk2.FamilyIsEEPROM())
					{ // need wider address
						columnCount = 17;
						//dataGridProgramMemory.Columns[0].Width = 51; // address column
						dataGridProgramMemory.Columns[0].Width = (int)(51 * ScalefactW); // address column
						dataColumns = 16;
						//columnWidth = 27;                    
						columnWidth = (int)(27 * ScalefactW);
					}
					else
					{
						columnCount = 17;
						//dataGridProgramMemory.Columns[0].Width = 35; // address column
						dataGridProgramMemory.Columns[0].Width = (int)(35 * ScalefactW); // address column
						dataColumns = 16;
						//columnWidth = 28;
						columnWidth = (int)(28 * ScalefactW);
					}
				}
				else if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32
					columnCount = 5;
					dataGridProgramMemory.Columns[0].Width = (int)(99 * ScalefactW); // address column
					dataColumns = 4;
					columnWidth = (int)(96 * ScalefactW);
				}
				else
				{
					columnCount = 9;
					//dataGridProgramMemory.Columns[0].Width = 59; // address column
					dataGridProgramMemory.Columns[0].Width = (int)(59 * ScalefactW); // address column
					dataColumns = 8;
					//columnWidth = 53;
					columnWidth = (int)(53 * ScalefactW);
				}

				if (dataGridProgramMemory.ColumnCount != columnCount)
				{
					dataGridProgramMemory.Rows.Clear();
					dataGridProgramMemory.ColumnCount = columnCount;
				}

				for (int column = 1; column < dataGridProgramMemory.ColumnCount; column++)
				{
					dataGridProgramMemory.Columns[column].Width = columnWidth; // data columns
				}

				int addressIncrement = (int)Pk2.DevFile.Families[Pk2.GetActiveFamily()].AddressIncrement;
				int rowAddressIncrement;
				int hexColumns, rowCount;
				if (comboBoxProgMemView.SelectedIndex == 0) // hex view
				{
					hexColumns = dataColumns;
					rowCount = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem / hexColumns;
					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem % hexColumns) > 0)
					{
						rowCount++;
					}
					rowAddressIncrement = addressIncrement * dataColumns;
				}
				else
				{
					hexColumns = dataColumns / 2;
					rowCount = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem / hexColumns;
					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem % hexColumns) > 0)
					{
						rowCount++;
					}
					rowAddressIncrement = addressIncrement * (dataColumns / 2);
				}
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32 - add rows for memory section titles
					rowCount += 2;
				}
				if (dataGridProgramMemory.RowCount != rowCount)
				{
					dataGridProgramMemory.Rows.Clear();
					dataGridProgramMemory.RowCount = rowCount;
				}

				for (int col = 0; col < hexColumns; col++)
				{ // hex editable
					dataGridProgramMemory.Columns[col + 1].ReadOnly = false;
				}

				int maxAddress = dataGridProgramMemory.RowCount * rowAddressIncrement - 1;
				String addressFormat = "{0:X3}";
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32
					addressFormat = "{0:X8}";
				}
				else if (maxAddress > 0xFFFF)
				{
					addressFormat = "{0:X5}";
				}
				else if (maxAddress > 0xFFF)
				{
					addressFormat = "{0:X4}";
				}
				// PIC32 info variables
				int progMemP32 = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem;
				int bootMemP32 = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].BootFlash;
				progMemP32 -= bootMemP32; // boot flash at upper end of prog mem.
				progMemP32 /= hexColumns;
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32 - insert titles
					dataGridProgramMemory.ShowCellToolTips = false;
					// Program Flash addresses
					dataGridProgramMemory[0, 0].Value = "Program Flash";
					for (int col = 0; col < dataGridProgramMemory.ColumnCount; col++)
					{
						dataGridProgramMemory[col, 0].Style.BackColor = System.Drawing.SystemColors.ControlDark;
						dataGridProgramMemory[col, 0].ReadOnly = true;
					}
					for (int row = 1, address = (int)KONST.P32_PROGRAM_FLASH_START_ADDR; row <= progMemP32; row++)
					{
						dataGridProgramMemory[0, row].Value = string.Format(addressFormat, address);
						dataGridProgramMemory[0, row].Style.BackColor = System.Drawing.SystemColors.ControlLight;
						address += rowAddressIncrement;
					}
					// Boot Flash addresses
					dataGridProgramMemory[0, progMemP32 + 1].Value = "Boot Flash";
					for (int col = 0; col < dataGridProgramMemory.ColumnCount; col++)
					{
						dataGridProgramMemory[col, progMemP32 + 1].Style.BackColor = System.Drawing.SystemColors.ControlDark;
						dataGridProgramMemory[col, progMemP32 + 1].ReadOnly = true;
					}
					for (int row = progMemP32 + 2, address = (int)KONST.P32_BOOT_FLASH_START_ADDR; row < dataGridProgramMemory.RowCount; row++)
					{
						dataGridProgramMemory[0, row].Value = string.Format(addressFormat, address);
						dataGridProgramMemory[0, row].Style.BackColor = System.Drawing.SystemColors.ControlLight;
						address += rowAddressIncrement;
					}
				}
				else
				{
					dataGridProgramMemory.ShowCellToolTips = true;
					for (int row = 0, address = 0; row < dataGridProgramMemory.RowCount; row++)
					{
						dataGridProgramMemory[0, row].Value = string.Format(addressFormat, address);
						dataGridProgramMemory[0, row].Style.BackColor = System.Drawing.SystemColors.ControlLight;
						address += rowAddressIncrement;
					}
				}
				//      Now fill data
				string dataFormat = "{0:X2}";
				int asciiBytes = 1;
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFF)
				{
					dataFormat = "{0:X3}";
					asciiBytes = 2;
				}
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFF)
				{
					dataFormat = "{0:X4}";
					asciiBytes = 2;
				}
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFF)
				{
					dataFormat = "{0:X6}";
					asciiBytes = 3;
				}
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{
					dataFormat = "{0:X8}";
					asciiBytes = 4;
				}
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32
					// Program Flash
					int idx = 0;
					for (int row = 1; row <= progMemP32; row++)
					{
						for (int col = 0; col < hexColumns; col++)
						{
							//dataGridProgramMemory[col + 1, row].ToolTipText =
							//    string.Format(addressFormat, (idx * addressIncrement));
							dataGridProgramMemory[col + 1, row].Value =
								string.Format(dataFormat, Pk2.DeviceBuffers.ProgramMemory[idx++]);
						}

					}
					// Boot Flash
					for (int row = progMemP32 + 2; row < dataGridProgramMemory.RowCount; row++)
					{
						for (int col = 0; col < hexColumns; col++)
						{
							//dataGridProgramMemory[col + 1, row].ToolTipText =
							//    string.Format(addressFormat, (idx * addressIncrement));
							dataGridProgramMemory[col + 1, row].Value =
								string.Format(dataFormat, Pk2.DeviceBuffers.ProgramMemory[idx++]);
						}

					}
				}
				else
				{
					for (int row = 0, idx = 0; row < (dataGridProgramMemory.RowCount - 1); row++)
					{ // all except last row
						for (int col = 0; col < hexColumns; col++)
						{
							dataGridProgramMemory[col + 1, row].ToolTipText =
								string.Format(addressFormat, (idx * addressIncrement));
							dataGridProgramMemory[col + 1, row].Value =
								string.Format(dataFormat, Pk2.DeviceBuffers.ProgramMemory[idx++]);
						}

					}
				}
				int lastrow = dataGridProgramMemory.RowCount - 1;
				int rowidx = lastrow * hexColumns;
				int lastcol = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem % hexColumns;
				if (lastcol == 0)
				{
					lastcol = hexColumns;
				}
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFFFF)
				{ // PIC32
					// "last row" not used for PIC32
				}
				else
				{
					for (int col = 0; col < hexColumns; col++)
					{ // fill last row
						if (col < lastcol)
						{
							dataGridProgramMemory[col + 1, lastrow].ToolTipText =
								string.Format(addressFormat, (rowidx * addressIncrement));
							dataGridProgramMemory[col + 1, lastrow].Value =
								string.Format(dataFormat, Pk2.DeviceBuffers.ProgramMemory[rowidx++]);
						}
						else
						{
							dataGridProgramMemory[col + 1, lastrow].ReadOnly = true;
						}
					}
				}

				//      Fill ASCII if selected
				if (comboBoxProgMemView.SelectedIndex >= 1)
				{
					for (int col = 0; col < hexColumns; col++)
					{ // ascii not editable
						dataGridProgramMemory.Columns[col + hexColumns + 1].ReadOnly = true;
					}
					if (comboBoxProgMemView.SelectedIndex == 1)
					{ //word view
						for (int row = 0, idx = 0; row < dataGridProgramMemory.RowCount; row++)
						{
							for (int col = 0; col < hexColumns; col++)
							{
								dataGridProgramMemory[col + hexColumns + 1, row].ToolTipText =
									string.Format(addressFormat, (idx * addressIncrement));

								dataGridProgramMemory[col + hexColumns + 1, row].Value =
									UTIL.ConvertIntASCII((int)Pk2.DeviceBuffers.ProgramMemory[idx++], asciiBytes);
							}

						}
					}
					else
					{ //byte view
						for (int row = 0, idx = 0; row < dataGridProgramMemory.RowCount; row++)
						{
							for (int col = 0; col < hexColumns; col++)
							{
								dataGridProgramMemory[col + hexColumns + 1, row].ToolTipText =
									string.Format(addressFormat, (idx * addressIncrement));
								dataGridProgramMemory[col + hexColumns + 1, row].Value =
									UTIL.ConvertIntASCIIReverse((int)Pk2.DeviceBuffers.ProgramMemory[idx++], asciiBytes);

							}

						}
					}
				}

				if ((dataGridProgramMemory.FirstDisplayedCell != null) && !progMemJustEdited)
				{
					//currentCol = dataGridProgramMemory.FirstDisplayedCell.ColumnIndex;
					int currentRow = dataGridProgramMemory.FirstDisplayedCell.RowIndex;
					dataGridProgramMemory.MultiSelect = false;
					dataGridProgramMemory[0, currentRow].Selected = true;              // these 4 statements remove the "select" box
					dataGridProgramMemory[0, currentRow].Selected = false;
					dataGridProgramMemory.MultiSelect = true;
				}
				else if (dataGridProgramMemory.FirstDisplayedCell == null)
				{ // remove select box when app first opened.
					dataGridProgramMemory.MultiSelect = false;
					dataGridProgramMemory[0, 0].Selected = true;              // these 4 statements remove the "select" box
					dataGridProgramMemory[0, 0].Selected = false;
					dataGridProgramMemory.MultiSelect = true;
				}
				progMemJustEdited = false;

			}


			// Update EEPROM display
			if (updateMemories && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
			{
				// prog mem checkbox only active when EE exists
				checkBoxProgMemEnabled.Enabled = true;
				comboBoxEE.Enabled = true;
				if (!checkBoxEEMem.Enabled)
				{ // if we're just enabling it
					checkBoxEEMem.Checked = true;
					checkBoxEEDATAMemoryEnabledAlt.Checked = true;
				}
				checkBoxEEMem.Enabled = true;
				enableDataProtectStripMenuItem.Enabled = true;
				checkBoxEEDATAMemoryEnabledAlt.Enabled = true;
				checkBoxProgMemEnabledAlt.Enabled = true;

				if (multiWindow)
				{
					// init multi window
					if (!eepromDataMultiWin.InitDone)
						eepromDataMultiWin.InitMemDisplay(comboBoxEE.SelectedIndex);
					// update
					if (!toolStripMenuItemShowEEPROMData.Enabled)
					{
						toolStripMenuItemShowEEPROMData.Enabled = true;
						if (multiWinEEMemOpen)
						{
							eepromDataMultiWin.Show();
							this.Focus();
						}
					}

					eepromDataMultiWin.UpdateMultiWinMem();
				}
				else
				{
					dataGridViewEEPROM.Visible = true;
					//      Set address columns first       
					//      Set # columns based on blank memory size.
					int rowAddressIncrement = (int)Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemAddressIncrement;
					int addressIncrement = rowAddressIncrement;
					int dataColumns, columnWidth, columnCount;
					if ((rowAddressIncrement == 1) && (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue != 0xFFF))
					{
						columnCount = 17;

						//dataGridViewEEPROM.Columns[0].Width = 32; // address column
						dataGridViewEEPROM.Columns[0].Width = (int)(32 * ScalefactW); // address column
						dataColumns = 16;
						//columnWidth = 21;                
						columnWidth = (int)(21 * ScalefactW);
					}
					else
					{ // 16-bit parts and basline
						columnCount = 9;
						//dataGridViewEEPROM.Columns[0].Width = 40; // address column
						dataGridViewEEPROM.Columns[0].Width = (int)(40 * ScalefactW); // address column
						dataColumns = 8;
						//columnWidth = 41;
						columnWidth = (int)(41 * ScalefactW);
					}
					if (dataGridViewEEPROM.ColumnCount != columnCount)
					{
						dataGridViewEEPROM.Rows.Clear();
						dataGridViewEEPROM.ColumnCount = columnCount;
					}

					dataGridViewEEPROM.Columns[0].ReadOnly = true;

					for (int column = 1; column < dataGridViewEEPROM.ColumnCount; column++)
					{
						dataGridViewEEPROM.Columns[column].Width = columnWidth; // data columns
					}

					int hexColumns, rowCount;
					if (comboBoxEE.SelectedIndex == 0) // hex view
					{
						hexColumns = dataColumns;

						rowCount = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem / hexColumns;
						rowAddressIncrement *= dataColumns;
						hexColumns = dataColumns;
					}
					else
					{
						hexColumns = dataColumns / 2;
						rowCount = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem / hexColumns;
						rowAddressIncrement *= (dataColumns / 2);
					}
					if (dataGridViewEEPROM.RowCount != rowCount)
					{
						dataGridViewEEPROM.Rows.Clear();
						dataGridViewEEPROM.RowCount = rowCount;
					}

					int maxAddress = dataGridViewEEPROM.RowCount * rowAddressIncrement - 1;
					String addressFormat = "{0:X2}";
					if (maxAddress > 0xFF)
					{
						addressFormat = "{0:X3}";
					}
					else if (maxAddress > 0xFFF)
					{
						addressFormat = "{0:X4}";
					}
					for (int row = 0, address = 0; row < dataGridViewEEPROM.RowCount; row++)
					{
						dataGridViewEEPROM[0, row].Value = string.Format(addressFormat, address);
						dataGridViewEEPROM[0, row].Style.BackColor = System.Drawing.SystemColors.ControlLight;
						address += rowAddressIncrement;
					}
					//      Now fill data
					string dataFormat = "{0:X2}";
					int asciiBytes = 1;
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemAddressIncrement > 1)
					{
						dataFormat = "{0:X4}";
						asciiBytes = 2;
					}
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue == 0xFFF)
					{ // baseline with data flash
						dataFormat = "{0:X3}";
						asciiBytes = 2;
					}
					for (int col = 0; col < hexColumns; col++)
					{ // hex editable
						dataGridViewEEPROM.Columns[col + 1].ReadOnly = false;
					}
					for (int row = 0, idx = 0; row < dataGridViewEEPROM.RowCount; row++)
					{
						for (int col = 0; col < hexColumns; col++)
						{
							dataGridViewEEPROM[col + 1, row].ToolTipText =
								string.Format(addressFormat, (idx * addressIncrement));
							dataGridViewEEPROM[col + 1, row].Value =
								string.Format(dataFormat, Pk2.DeviceBuffers.EEPromMemory[idx++]);
						}

					}
					//      Fill ASCII if selected
					if (comboBoxEE.SelectedIndex >= 1)
					{
						for (int col = 0; col < hexColumns; col++)
						{ // ascii not editable
							dataGridViewEEPROM.Columns[col + hexColumns + 1].ReadOnly = true;
						}
						if (comboBoxEE.SelectedIndex == 1)
						{ // word view    
							for (int row = 0, idx = 0; row < dataGridViewEEPROM.RowCount; row++)
							{
								for (int col = 0; col < hexColumns; col++)
								{
									dataGridViewEEPROM[col + hexColumns + 1, row].ToolTipText =
										string.Format(addressFormat, (idx * addressIncrement));
									dataGridViewEEPROM[col + hexColumns + 1, row].Value =
										UTIL.ConvertIntASCII((int)Pk2.DeviceBuffers.EEPromMemory[idx++], asciiBytes);
								}

							}
						}
						else
						{ //byte view
							for (int row = 0, idx = 0; row < dataGridViewEEPROM.RowCount; row++)
							{
								for (int col = 0; col < hexColumns; col++)
								{
									dataGridViewEEPROM[col + hexColumns + 1, row].ToolTipText =
										string.Format(addressFormat, (idx * addressIncrement));
									dataGridViewEEPROM[col + hexColumns + 1, row].Value =
										UTIL.ConvertIntASCIIReverse((int)Pk2.DeviceBuffers.EEPromMemory[idx++], asciiBytes);
								}

							}
						}
					}
					if ((dataGridViewEEPROM.FirstDisplayedCell != null) && !eeMemJustEdited)
					{
						//currentCol = dataGridViewEEPROM.FirstDisplayedCell.ColumnIndex;
						int currentRow = dataGridViewEEPROM.FirstDisplayedCell.RowIndex;
						dataGridViewEEPROM.MultiSelect = false;
						dataGridViewEEPROM[0, currentRow].Selected = true;              // these 4 statements remove the "select" box
						dataGridViewEEPROM[0, currentRow].Selected = false;
						dataGridViewEEPROM.MultiSelect = true;
					}
					else if (dataGridViewEEPROM.FirstDisplayedCell == null)
					{ // remove select box when app first opened.
						dataGridViewEEPROM.MultiSelect = false;
						dataGridViewEEPROM[0, 0].Selected = true;              // these 4 statements remove the "select" box
						dataGridViewEEPROM[0, 0].Selected = false;
						dataGridViewEEPROM.MultiSelect = true;
					}
					eeMemJustEdited = false;
				}
			}
			else if (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem == 0)
			{
				dataGridViewEEPROM.Visible = false;
				comboBoxEE.Enabled = false;
				checkBoxEEMem.Checked = false;
				checkBoxEEDATAMemoryEnabledAlt.Checked = false;
				checkBoxEEMem.Enabled = false;
				checkBoxEEDATAMemoryEnabledAlt.Enabled = false;
				enableDataProtectStripMenuItem.Enabled = false;
				enableDataProtectStripMenuItem.Checked = false;
				checkBoxProgMemEnabled.Checked = true;
				checkBoxProgMemEnabledAlt.Checked = true;
				checkBoxProgMemEnabled.Enabled = false;
				checkBoxProgMemEnabledAlt.Enabled = false;
				eepromDataMultiWin.Hide();
				toolStripMenuItemShowEEPROMData.Enabled = false;
			}


			// Update "Code Protect" label
			// First check to see if loaded data has code protects asserted.
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask != 0) && ((Pk2.DeviceBuffers.ConfigWords[Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1] &
				Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask) != Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask))
			{ // loaded hex has CP asserted
				enableCodeProtectToolStripMenuItem.Checked = true;
				enableCodeProtectToolStripMenuItem.Enabled = false; // can't "deassert" CP in hex
				if ((Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0)
					&& (Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask == 0))
				{// if part has EE but no CPMask, then both CP/DP get selected at same time (ie dsPIC30)
					enableDataProtectStripMenuItem.Checked = true;
					enableDataProtectStripMenuItem.Enabled = false;
				}
			}
			// Then check for data protect asserted
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0) && ((Pk2.DeviceBuffers.ConfigWords[Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1] &
				Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask) != Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask))
			{
				enableDataProtectStripMenuItem.Checked = true;
				enableDataProtectStripMenuItem.Enabled = false;
			}
			if ((enableCodeProtectToolStripMenuItem.Checked) || (enableDataProtectStripMenuItem.Checked))
			{
				labelCodeProtect.Visible = true;
				if ((enableCodeProtectToolStripMenuItem.Checked) && (enableDataProtectStripMenuItem.Checked))
				{
					labelCodeProtect.Text = "All Protect";
				}
				else if (enableCodeProtectToolStripMenuItem.Checked)
				{
					labelCodeProtect.Text = "Code Protect";
				}
				else
				{
					labelCodeProtect.Text = "Data Protect";
				}
			}
			else
			{
				labelCodeProtect.Visible = false;
			}

			// Update EEPROM status label
			if (!checkBoxProgMemEnabled.Checked)
			{
				displayEEProgInfo.Text = "Write and Read EEPROM data only.";
				displayEEProgInfo.Visible = true;
				eepromDataMultiWin.DisplayEETextOn("Write and Read EEPROM data only.");
			}
			else if (!checkBoxEEMem.Checked && checkBoxEEMem.Enabled)
			{
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemEraseScript != 0)
				{
					displayEEProgInfo.Text = "Preserve device EEPROM data on write.";
					eepromDataMultiWin.DisplayEETextOn("Preserve device EEPROM data on write.");
				}
				else
				{
					displayEEProgInfo.Text = "Read/Restore device EEPROM on write.";
					eepromDataMultiWin.DisplayEETextOn("Read/Restore device EEPROM on write.");
				}
				displayEEProgInfo.Visible = true;
			}
			else
			{
				displayEEProgInfo.Visible = false;
				eepromDataMultiWin.DisplayEETextOff();
			}

			// update test memory if being used
			if (TestMemoryEnabled && TestMemoryOpen)
			{
				formTestMem.UpdateTestMemForm();
				if (updateMemories)
				{
					formTestMem.UpdateTestMemoryGrid();
				}
			}

			// update test forms if connected
			if (testConnected)
			{
				updateTestGUI();
			}
		}

		private void updateTestGUI()
		{

		}

		private void progMemViewChanged(object sender, EventArgs e)
		{
			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private void buildDeviceMenu()
		{
			// Search through all families and display in order of "family type"
			// from lowest to highest.
			for (int familyOrder = 0; familyOrder < Pk2.DevFile.Families.Length; familyOrder++)
			{
				for (int checkFamily = 0; checkFamily < Pk2.DevFile.Families.Length; checkFamily++)
				{
					if (Pk2.DevFile.Families[checkFamily].FamilyType == familyOrder)
					{
						string familyName = Pk2.DevFile.Families[checkFamily].FamilyName;
						int subName = familyName.IndexOf("/");
						if (familyName[0] != '#') // skip families beginning with pound sign
						{
							if (subName < 0)  // submenu = -1 if not found
							{
								deviceToolStripMenuItem.DropDown.Items.Add(familyName);
							}
							else
							{
								int numItems = deviceToolStripMenuItem.DropDownItems.Count;
								for (int menuIndex = 0; menuIndex < numItems; menuIndex++)
								{
									if (familyName.Substring(0, subName)
										== deviceToolStripMenuItem.DropDown.Items[menuIndex].ToString())
									{
										ToolStripMenuItem subItem = (ToolStripMenuItem)deviceToolStripMenuItem.DropDownItems[menuIndex];
										subItem.DropDown.Items.Add(familyName.Substring(subName + 1));
									}
									else if (menuIndex == (numItems - 1))
									{
										deviceToolStripMenuItem.DropDown.Items.Add(familyName.Substring(0, subName));
										ToolStripMenuItem subItem = (ToolStripMenuItem)deviceToolStripMenuItem.DropDownItems[menuIndex + 1];
										subItem.DropDown.Items.Add(familyName.Substring(subName + 1));
										subItem.DropDownItemClicked += new ToolStripItemClickedEventHandler(deviceFamilyClick);
									}
								}
							}
						}
					}
				}

			}

			deviceToolStripMenuItem.Enabled = true;
		}


		private void guiVddControl(object sender, EventArgs e)
		{
			vddControl(false, false);
		}

		private void vddControl(bool force, bool forceState)
		{
			// checkbox state is new state.  
			if (force)
			{
				chkBoxVddOn.Checked = forceState;
			}
			bool vddNowOn = chkBoxVddOn.Checked;

			if (detectPICkit2(KONST.NoMessage, false))
			{
				if (vddNowOn)
				{
					// check for a self-powered target first
					if (!lookForPoweredTarget(KONST.ShowMessage))
					{ // don't execute if self-powered found
						chkBoxVddOn.Checked = true;
						Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);   // make sure voltage is set
						Pk2.VddOn();

						if (checkForPowerErrors())
						{
							Pk2.VddOff();
						}
					}
					else
					{
						checkForPowerErrors();
						Pk2.VddOff();
					}
				}
				else
				{
					chkBoxVddOn.Checked = false;
					Pk2.VddOff();
				}
			}
		}

		private void guiChangeVDD(object sender, EventArgs e)
		{
			if (detectPICkit2(KONST.NoMessage, false))
			{
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);
			}
		}

		private void pickitFormClosing(object sender, FormClosingEventArgs e)
		{
			SaveINI();
		}

		private void fileMenuExit(object sender, EventArgs e)
		{
			this.Close();
		}

		private void menuFileImportHex(object sender, EventArgs e)
		{
			// don't need to check for PICkit 2 or device when importing.
			// This will be detected when the user attempts a programming command.         

			if (Pk2.FamilyIsKeeloq())
			{ // can import first line of NUM files
				openHexFileDialog.Filter = "HEX files|*.hex;*.num|All files|*.*";
			}
			else if (Pk2.FamilyIsEEPROM())
			{
				openHexFileDialog.Filter = "HEX or BIN files|*.hex;*.bin|All files|*.*";
			}
			else
			{
				openHexFileDialog.Filter = "HEX files|*.hex|All files|*.*";
			}

			openHexFileDialog.ShowDialog();

			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private void importHexFile(object sender, CancelEventArgs e)
		{
			importHexFileGo();
		}

		private bool importHexFileGo()
		{
			int lastPart = Pk2.ActivePart;  // save last part, if part doesn't change keep buffers.
			bool verifySave = deviceVerification;
			deviceVerification = false;     // in manual mode, don't check for device on import.

			if (!preProgrammingCheck(Pk2.GetActiveFamily()))
			{
				deviceVerification = verifySave;
				displayStatusWindow.Text = "Device Error - hex file not loaded.";
				statusWindowColor = Constants.StatusColor.red;
				displayDataSource.Text = "None.";
				importGo = false;
				return false;
			}

			deviceVerification = verifySave;

			// clear device buffers.
			if ((lastPart != Pk2.ActivePart)                            // a new part is detected
				|| (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem == 0)   // the part has no EE Data
				|| (checkBoxProgMemEnabled.Checked && checkBoxEEMem.Checked)) // Both memory regions are checked
			{ // reset everything
				Pk2.ResetBuffers();
			}
			else
			{ // just clear checked regions
				if (checkBoxProgMemEnabled.Checked)
				{
					Pk2.DeviceBuffers.ClearProgramMemory(Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue);
					Pk2.DeviceBuffers.ClearConfigWords(Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigBlank);
					Pk2.DeviceBuffers.ClearUserIDs(Pk2.DevFile.Families[Pk2.GetActiveFamily()].UserIDBytes,
													Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue);
				}
				if (checkBoxEEMem.Checked)
				{
					Pk2.DeviceBuffers.ClearEEPromMemory(Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemAddressIncrement,
														Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue);
				}

			}

			if (TestMemoryEnabled && TestMemoryOpen)
			{
				if (formTestMem.HexImportExportTM())
				{
					formTestMem.ClearTestMemory();
				}
			}

			string fileType = "Hex";
			if ((openHexFileDialog.FileName.Substring(openHexFileDialog.FileName.Length - 4).ToUpper() == ".BIN") && Pk2.FamilyIsEEPROM())
				fileType = "Bin";

			switch (ImportExportHex.ImportHexFile(openHexFileDialog.FileName, checkBoxProgMemEnabled.Checked, checkBoxEEMem.Checked))
			{
				case Constants.FileRead.success:
					displayStatusWindow.Text = fileType + " file sucessfully imported.";
					if (multiWindow)
						displayDataSource.Text = openHexFileDialog.FileName;
					else
						displayDataSource.Text = shortenHex(openHexFileDialog.FileName);
					checkImportFile = true;
					importGo = true;
					break;

				case Constants.FileRead.noconfig:
					statusWindowColor = Constants.StatusColor.yellow;
					displayStatusWindow.Text =
						"Warning: No configuration words in hex file.\nIn MPLAB use File-Export to save hex with config.";
					if (multiWindow)
						displayDataSource.Text = openHexFileDialog.FileName;
					else
						displayDataSource.Text = shortenHex(openHexFileDialog.FileName);
					checkImportFile = true;
					importGo = true;
					break;

				case Constants.FileRead.partialcfg:
					statusWindowColor = Constants.StatusColor.yellow;
					displayStatusWindow.Text =
						"Warning: Some configuration words not in hex file.\nEnsure default values above right are acceptable.";
					if (multiWindow)
						displayDataSource.Text = openHexFileDialog.FileName;
					else
						displayDataSource.Text = shortenHex(openHexFileDialog.FileName);
					checkImportFile = true;
					importGo = true;
					break;

				case Constants.FileRead.largemem:
					statusWindowColor = Constants.StatusColor.yellow;
					displayStatusWindow.Text = "Warning: " + fileType + " File Loaded is larger than device.";
					if (multiWindow)
						displayDataSource.Text = openHexFileDialog.FileName;
					else
						displayDataSource.Text = shortenHex(openHexFileDialog.FileName);
					checkImportFile = true;
					importGo = true;
					break;

				default:
					statusWindowColor = Constants.StatusColor.red;
					displayStatusWindow.Text = "Error reading " + fileType + " file.";
					displayDataSource.Text = "None (Empty/Erased)";
					checkImportFile = false;
					importGo = false;
					Pk2.ResetBuffers();
					break;

			}

			if (checkImportFile)
			{
				// Get OSCCAL if need be
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
				{
					Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
					Pk2.VddOn();
					Pk2.ReadOSSCAL();
					Pk2.DeviceBuffers.ProgramMemory[Pk2.DeviceBuffers.ProgramMemory.Length - 1] =
							Pk2.DeviceBuffers.OSCCAL;
				}

				// Get BandGap if need be
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
				{
					Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
					Pk2.VddOn();
					Pk2.ReadBandGap();
				}
				conditionalVDDOff();

				// Add file to quick hex import links
				bool fallThru = false;
				bool foundFile = false;
				do
				{
					if ((openHexFileDialog.FileName == hex4) || fallThru)
					{
						if ((!hex4ToolStripMenuItem.Visible) && (hex3.Length > 3))
						{
							hex4ToolStripMenuItem.Visible = true;
						}
						hex4 = hex3;
						hex4ToolStripMenuItem.Text = "&4" + hex3ToolStripMenuItem.Text.Substring(2);
						fallThru = true;
						foundFile = true;
					}
					if ((openHexFileDialog.FileName == hex3) || fallThru)
					{
						if ((!hex3ToolStripMenuItem.Visible) && (hex2.Length > 3))
						{
							hex3ToolStripMenuItem.Visible = true;
						}
						hex3 = hex2;
						hex3ToolStripMenuItem.Text = "&3" + hex2ToolStripMenuItem.Text.Substring(2);
						fallThru = true;
						foundFile = true;
					}
					if ((openHexFileDialog.FileName == hex2) || fallThru)
					{
						if ((!hex2ToolStripMenuItem.Visible) && (hex1.Length > 3))
						{
							hex2ToolStripMenuItem.Visible = true;
						}
						hex2 = hex1;
						hex2ToolStripMenuItem.Text = "&2" + hex1ToolStripMenuItem.Text.Substring(2);
						foundFile = true;
					}
					fallThru = true;
					if (openHexFileDialog.FileName == hex1)
					{
						foundFile = true;
					}
				} while (!foundFile);

				if (!hex1ToolStripMenuItem.Visible)
				{
					hex1ToolStripMenuItem.Visible = true;
					toolStripMenuItem5.Visible = true;
				}
				hex1 = openHexFileDialog.FileName;
				hex1ToolStripMenuItem.Text = "&1 " + shortenHex(openHexFileDialog.FileName);
			}

			return checkImportFile;

		}

		private void menuFileExportHex(object sender, EventArgs e)
		{
			if (Pk2.FamilyIsKeeloq())
			{
				MessageBox.Show("Export not supported for\nthis part type.");
			}
			else
			{
				if (Pk2.FamilyIsEEPROM())
				{
					saveHexFileDialog.Filter = "Hex files|*.hex|Bin Files|*.bin|All files|*.*";
				}
				else
				{
					saveHexFileDialog.Filter = "Hex files|*.hex|All files|*.*";
				}

				saveHexFileDialog.ShowDialog();
			}
		}

		private void exportHexFile(object sender, CancelEventArgs e)
		{
			ImportExportHex.ExportHexFile(saveHexFileDialog.FileName, checkBoxProgMemEnabled.Checked, checkBoxEEMem.Checked);
		}

		private bool preProgrammingCheck(int family)
		{
			statusGroupBox.Update();

			if (Pk2.LearnMode)
			{
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);  // ensure voltage set to expected value.
				return true;
			}

			if (!detectPICkit2(KONST.NoMessage, false))
			{
				return false;
			}

			if (checkForPowerErrors())
			{
				updateGUI(KONST.DontUpdateMemDisplays);
				return false;
			}

			lookForPoweredTarget(KONST.ShowMessage & !timerAutoImportWrite.Enabled);
			// don't show message if AutoImportWrite mode enabled.

			if (Pk2.DevFile.Families[family].PartDetect)
			{
				if (Pk2.DetectDevice(family, false, chkBoxVddOn.Checked))
				{
					setGUIVoltageLimits(false);
					fullEnableGUIControls();
					updateGUI(KONST.DontUpdateMemDisplays);
				}
				else
				{
					semiEnableGUIControls();
					statusWindowColor = Constants.StatusColor.yellow;
					displayStatusWindow.Text = "No device detected.";
					if (Pk2.DevFile.Families[family].Vpp < 1)
					{// PIC18J, PIC24, dsPIC33
						// remind about VCAP
						displayStatusWindow.Text += "\nEnsure proper capacitance on VDDCORE/VCAP pin.";
					}
					checkForPowerErrors();
					updateGUI(KONST.DontUpdateMemDisplays);
					return false;
				}
			}
			else if ((Pk2.DevFile.Families[family].DeviceIDMask > 0) && deviceVerification)
			{ // device's that have IDs, but are in Manual Select mode.
				if (!Pk2.VerifyDeviceID(false, chkBoxVddOn.Checked))
				{
					statusWindowColor = Constants.StatusColor.yellow;
					if ((Pk2.LastDeviceID == 0) || (Pk2.LastDeviceID == Pk2.DevFile.Families[family].DeviceIDMask))
					{
						displayStatusWindow.Text = "No device detected.";
					}
					else
					{
						displayStatusWindow.Text = "Selected device not detected.";
						for (int part = 0; part < Pk2.DevFile.PartsList.Length; part++)
						{
							if ((Pk2.DevFile.PartsList[part].Family == family) && (Pk2.DevFile.PartsList[part].DeviceID == Pk2.LastDeviceID))
							{
								displayStatusWindow.Text += "\nDetected a " + Pk2.DevFile.PartsList[part].PartName + " instead.";
								break;
							}
						}
					}
					checkForPowerErrors();
					updateGUI(KONST.DontUpdateMemDisplays);
					return false;
				}

			}
			else
			{
				Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);  // ensure voltage set
				Pk2.VddOn();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				Thread.Sleep(300);                      // give some delay for error to develop
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				conditionalVDDOff();
				if (checkForPowerErrors())
				{
					updateGUI(KONST.DontUpdateMemDisplays);
					return false;
				}
			}
			Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);  // ensure voltage set to expected value.
			if (!checkBoxEEMem.Enabled && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
			{
				// if previous part had no EEPROM and this one does, make sure EE checkbox is checked
				// otherwise EEPROM won't be read or imported if these are the first operations with a new part.
				checkBoxEEMem.Checked = true;
			}
			return true;
		}

		/**********************************************************************************************************
		 **********************************************************************************************************
		 ***                                                                                                    ***
		 ***                                           READ DEVICE                                              ***
		 ***                                                                                                    *** 
		 ********************************************************************************************************** 
		 **********************************************************************************************************/


		private void readDevice(object sender, EventArgs e)
		{
			deviceRead();
		}

		private void deviceRead()
		{
			if (Pk2.FamilyIsKeeloq())
			{
				displayStatusWindow.Text = "Read not supported for this device type.";
				statusWindowColor = Constants.StatusColor.yellow;
				updateGUI(KONST.DontUpdateMemDisplays);
				return; // abort
			}

			if (!preProgrammingCheck(Pk2.GetActiveFamily()))
			{
				return; // abort
			}

			if (Pk2.FamilyIsPIC32())
			{
				if (P32.PIC32Read())
				{
					statusWindowColor = Constants.StatusColor.normal;
				}
				else
				{
					statusWindowColor = Constants.StatusColor.red;
				}
				conditionalVDDOff();
				updateGUI(KONST.UpdateMemoryDisplays);
				return;
			}

			displayStatusWindow.Text = "Reading device:\n";
			//displayStatusWindow.Update();
			this.Update();

			byte[] upload_buffer = new byte[KONST.UploadBufferSize];

			Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
			Pk2.VddOn();

			// Read Program Memory
			if (checkBoxProgMemEnabled.Checked)
			{
				displayStatusWindow.Text += "Program Memory... ";
				this.Update();

				if (useProgExec33())
				{
					if (!PE33.PE33Read(displayStatusWindow.Text))
					{
						statusWindowColor = Constants.StatusColor.red;
						conditionalVDDOff();
						updateGUI(KONST.UpdateMemoryDisplays);
						return;
					}
				}
				else if (useProgExec24F())
				{
					if (!PE24.PE24FRead(displayStatusWindow.Text))
					{
						statusWindowColor = Constants.StatusColor.red;
						conditionalVDDOff();
						updateGUI(KONST.UpdateMemoryDisplays);
						return;
					}
				}
				else
				{

					Pk2.RunScript(KONST.PROG_ENTRY, 1);

					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrSetScript != 0)
							&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrBytes != 0))
					{ // if prog mem address set script exists for this part & # bytes is not zero
						// (MPLAB uses script on some parts when PICkit 2 does not)
						if (Pk2.FamilyIsEEPROM())
						{
							Pk2.DownloadAddress3MSBFirst(eeprom24BitAddress(0, KONST.WRITE_BIT));
							Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
							if (eeprom_CheckBusErrors())
							{
								return;
							}
						}
						else
						{
							Pk2.DownloadAddress3(0);
							//Pk2.DownloadAddress3(0x800000);
							Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
						}
					}

					int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation;
					int scriptRunsToFillUpload = KONST.UploadBufferSize /
						(Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords * bytesPerWord);
					int wordsPerLoop = scriptRunsToFillUpload * Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords;
					int wordsRead = 0;

					progressBar1.Value = 0;     // reset bar
					progressBar1.Maximum = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem / wordsPerLoop;

					do
					{
						if (Pk2.FamilyIsEEPROM())
						{
							if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
								&& (wordsRead > Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG])
								&& (wordsRead % (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] + 1) == 0))
							{ // must resend address to EE every time we cross a bank border.
								Pk2.DownloadAddress3MSBFirst(eeprom24BitAddress(wordsRead, KONST.WRITE_BIT));
								Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
							}
							Pk2.Download3Multiples(eeprom24BitAddress(wordsRead, KONST.READ_BIT), scriptRunsToFillUpload,
										Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords);
						}
						//Pk2.RunScriptUploadNoLen2(KONST.PROGMEM_RD, scriptRunsToFillUpload);
						Pk2.RunScriptUploadNoLen(KONST.PROGMEM_RD, scriptRunsToFillUpload);
						Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
						//Pk2.GetUpload();
						Pk2.UploadDataNoLen();
						Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
						int uploadIndex = 0;
						for (int word = 0; word < wordsPerLoop; word++)
						{
							int bite = 0;
							uint memWord = (uint)upload_buffer[uploadIndex + bite++];
							if (bite < bytesPerWord)
							{
								memWord |= (uint)upload_buffer[uploadIndex + bite++] << 8;
							}
							if (bite < bytesPerWord)
							{
								memWord |= (uint)upload_buffer[uploadIndex + bite++] << 16;
							}
							if (bite < bytesPerWord)
							{
								memWord |= (uint)upload_buffer[uploadIndex + bite++] << 24;
							}
							uploadIndex += bite;
							// shift if necessary
							if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
							{
								memWord = (memWord >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
							}
							Pk2.DeviceBuffers.ProgramMemory[wordsRead++] = memWord;
							if (wordsRead == Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem)
							{
								break; // for cases where ProgramMemSize%WordsPerLoop != 0
							}
							if (((wordsRead % 0x8000) == 0)
									&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrSetScript != 0)
									&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrBytes != 0)
									&& (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFF))
							{ //PIC24 must update TBLPAG
								Pk2.DownloadAddress3(0x10000 * (wordsRead / 0x8000));
								Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
								break;
							}
						}
						progressBar1.PerformStep();
					} while (wordsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem);

					Pk2.RunScript(KONST.PROG_EXIT, 1);

					// swap "Endian-ness" of 16 bit 93LC EEPROMs
					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.MICROWIRE_BUS)
								 && (bytesPerWord == 2) && (Pk2.FamilyIsEEPROM()))
					{
						uint memTemp = 0;
						for (int x = 0; x < Pk2.DeviceBuffers.ProgramMemory.Length; x++)
						{
							memTemp = (Pk2.DeviceBuffers.ProgramMemory[x] << 8) & 0xFF00;
							Pk2.DeviceBuffers.ProgramMemory[x] >>= 8;
							Pk2.DeviceBuffers.ProgramMemory[x] |= memTemp;
						}
					}
				}
			}


			// Read EEPROM
			if ((checkBoxEEMem.Checked) && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
			{
				readEEPROM();
			}


			// Read UserIDs
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords > 0) && checkBoxProgMemEnabled.Checked)
			{
				displayStatusWindow.Text += "UserIDs... ";
				this.Update();

				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDRdPrepScript > 0)
				{
					Pk2.RunScript(KONST.USERID_RD_PREP, 1);
				}
				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].UserIDBytes;
				int wordsRead = 0;
				int bufferIndex = 0;
				Pk2.RunScriptUploadNoLen(KONST.USERID_RD, 1);
				Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
				if ((Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords * bytesPerWord) > KONST.USB_REPORTLENGTH)
				{
					Pk2.UploadDataNoLen();
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
				}
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				do
				{
					int bite = 0;
					uint memWord = (uint)upload_buffer[bufferIndex + bite++];
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 8;
					}
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 16;
					}
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 24;
					}
					bufferIndex += bite;

					// shift if necessary
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						memWord = (memWord >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
					}
					Pk2.DeviceBuffers.UserIDs[wordsRead++] = memWord;
				} while (wordsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords);
			}

			// Read Configuration
			int configLocation = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigAddr /
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemHexBytes;
			int configWords = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords;
			if ((configWords > 0) && (configLocation >= Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem)
						&& checkBoxProgMemEnabled.Checked)
			{ // Don't read config words for any part where they are stored in program memory.
				displayStatusWindow.Text += "Config... ";
				//displayStatusWindow.Update();
				this.Update();
				Pk2.ReadConfigOutsideProgMem();

				// save bandgap if necessary
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
				{
					Pk2.DeviceBuffers.BandGap = Pk2.DeviceBuffers.ConfigWords[0] &
							Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask;
				}

			}
			else if ((configWords > 0) && checkBoxProgMemEnabled.Checked)
			{ // pull them out of program memory.
				displayStatusWindow.Text += "Config... ";
				//displayStatusWindow.Update();            
				this.Update();
				for (int word = 0; word < configWords; word++)
				{
					Pk2.DeviceBuffers.ConfigWords[word] = Pk2.DeviceBuffers.ProgramMemory[configLocation + word];
				}
			}

			// Read OSCCAL if exists
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
			{
				Pk2.ReadOSSCAL();
			}


			// Read Test Memory if open
			if (TestMemoryEnabled && TestMemoryOpen)
			{
				formTestMem.ReadTestMemory();
			}

			// TESTING for DEBUG vector Read
			//labelBandGap.Text = string.Format("{0:X8}", Pk2.ReadDebugVector());

			conditionalVDDOff();

			displayStatusWindow.Text += "Done.";

			// update SOURCE box
			displayDataSource.Text = "Read from " + Pk2.DevFile.PartsList[Pk2.ActivePart].PartName;
			checkImportFile = false;

			updateGUI(KONST.UpdateMemoryDisplays);

		}

		private void readEEPROM()
		{
			byte[] upload_buffer = new byte[KONST.UploadBufferSize];

			displayStatusWindow.Text += "EE... ";
			this.Update();

			Pk2.RunScript(KONST.PROG_ENTRY, 1);

			if (Pk2.DevFile.PartsList[Pk2.ActivePart].EERdPrepScript > 0)
			{
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemHexBytes == 4)
				{ // 16-bit parts
					Pk2.DownloadAddress3((int)(Pk2.DevFile.PartsList[Pk2.ActivePart].EEAddr / 2));
				}
				else
				{
					Pk2.DownloadAddress3(0);
				}
				Pk2.RunScript(KONST.EE_RD_PREP, 1);
			}

			int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemBytesPerWord;
			int scriptRunsToFillUpload = KONST.UploadBufferSize /
				(Pk2.DevFile.PartsList[Pk2.ActivePart].EERdLocations * bytesPerWord);
			int wordsPerLoop = scriptRunsToFillUpload * Pk2.DevFile.PartsList[Pk2.ActivePart].EERdLocations;
			int wordsRead = 0;

			uint eeBlank = getEEBlank();

			progressBar1.Value = 0;     // reset bar
			progressBar1.Maximum = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem / wordsPerLoop;
			do
			{
				//Pk2.RunScriptUploadNoLen2(KONST.EE_RD, scriptRunsToFillUpload);
				Pk2.RunScriptUploadNoLen(KONST.EE_RD, scriptRunsToFillUpload);
				Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
				//Pk2.GetUpload();
				Pk2.UploadDataNoLen();
				Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
				int uploadIndex = 0;
				for (int word = 0; word < wordsPerLoop; word++)
				{
					int bite = 0;
					uint memWord = (uint)upload_buffer[uploadIndex + bite++];
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[uploadIndex + bite++] << 8;
					}
					uploadIndex += bite;
					// shift if necessary
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						memWord = (memWord >> 1) & eeBlank;
					}
					Pk2.DeviceBuffers.EEPromMemory[wordsRead++] = memWord;
					if (wordsRead >= Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem)
					{
						break; // for cases where ProgramMemSize%WordsPerLoop != 0
					}
				}
				progressBar1.PerformStep();
			} while (wordsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem);
			Pk2.RunScript(KONST.PROG_EXIT, 1);
		}

		/**********************************************************************************************************
		 **********************************************************************************************************
		 ***                                                                                                    ***
		 ***                                          ERASE DEVICE                                              ***
		 ***                                                                                                    *** 
		 ********************************************************************************************************** 
		 **********************************************************************************************************/

		private void eraseDevice(object sender, EventArgs e)
		{
			//Pk2.TestingMethod();
			eraseDeviceAll(false, new uint[0]); // 
		}

		private void eraseDeviceAll(bool forceOSSCAL, uint[] calWords)
		{
			if (Pk2.FamilyIsKeeloq() || Pk2.FamilyIsMCP())
			{
				displayStatusWindow.Text = "Erase not supported for this device type.";
				statusWindowColor = Constants.StatusColor.yellow;
				updateGUI(KONST.DontUpdateMemDisplays);
				return; // abort
			}

			if (!preProgrammingCheck(Pk2.GetActiveFamily()))
			{
				return; // abort
			}

			//Save off the buffers if not clearing them
			DeviceData savedBuffers = Pk2.CloneBuffers(Pk2.DeviceBuffers);

			if ((Pk2.FamilyIsEEPROM()) && (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] != KONST.MICROWIRE_BUS)
				&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] != KONST.UNIO_BUS))
			{ // Microwire / UNIO have a true "chip erase".  Other devices must write every byte blank.
				Pk2.ResetBuffers();         // just erase buffers
				checkImportFile = false;
				if (!eepromWrite(KONST.EraseEE)) // and write blank value.
				{
					if (!toolStripMenuItemClearBuffersErase.Checked)
					{// restore buffers
						Pk2.DeviceBuffers = savedBuffers;
					}
					return; //abort.
				}
				displayStatusWindow.Text += "Complete";
				//displayStatusWindow.Update();
				if (!toolStripMenuItemClearBuffersErase.Checked)
				{// restore buffers
					Pk2.DeviceBuffers = savedBuffers;
				}
				else
				{
					displayDataSource.Text = "None (Empty/Erased)";
				}
				updateGUI(KONST.UpdateMemoryDisplays);
				return;
			}

			if (!checkEraseVoltage(false))
			{
				return; // abort
			}

			progressBar1.Value = 0;     // reset bar

			Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
			Pk2.VddOn();

			// Get OSCCAL if need be
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave) && !forceOSSCAL)
			{ // if forcing OSCCAL, don't read it; use the value in memory.
				Pk2.ReadOSSCAL();

				// verify OSCCAL
				if (!verifyOSCCAL())
				{
					return;
				}
			}
			uint oscCal = Pk2.DeviceBuffers.OSCCAL;

			// Get BandGap if need be
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
			{
				Pk2.ReadBandGap();
			}
			uint bandGap = Pk2.DeviceBuffers.BandGap;

			displayStatusWindow.Text = "Erasing device...";
			//displayStatusWindow.Update();
			this.Update();

			// dsPIC30F5011, 5013 need configs cleared before erase
			// but don't run this script if a row erase is defined
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMemEraseScript > 0)
				&& (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseSize == 0))
			{
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWrPrepScript > 0)
				{
					Pk2.DownloadAddress3(0);
					Pk2.RunScript(KONST.CONFIG_WR_PREP, 1);
				}
				Pk2.ExecuteScript(Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMemEraseScript);
				Pk2.RunScript(KONST.PROG_EXIT, 1);
			}

			Pk2.RunScript(KONST.PROG_ENTRY, 1);
			if (TestMemoryEnabled && TestMemoryOpen && (calWords.Length > 0))
			{ // write calibration words in midrange parts
				byte[] calBytes = new byte[2 * calWords.Length];

				for (int werd = 0; werd < calWords.Length; werd++)
				{
					calWords[werd] <<= 1;
					calBytes[2 * werd] = (byte)(calWords[werd] & 0xFF);
					calBytes[(2 * werd) + 1] = (byte)(calWords[werd] >> 8);
				}
				Pk2.DataClrAndDownload(calBytes, 0);
				Pk2.RunScript(KONST.OSSCAL_WR, 1);
			}
			else
			{
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ChipErasePrepScript > 0)
				{
					Pk2.RunScript(KONST.ERASE_CHIP_PREP, 1);
				}
				Pk2.RunScript(KONST.ERASE_CHIP, 1);
			}
			Pk2.RunScript(KONST.PROG_EXIT, 1);

			// clear all the buffers
			Pk2.ResetBuffers();
			if (TestMemoryEnabled && TestMemoryOpen && !toolStripMenuItemClearBuffersErase.Checked)
			{
				formTestMem.ClearTestMemory();
			}

			// restore OSCCAL if need be
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
			{
				Pk2.DeviceBuffers.OSCCAL = oscCal;
				savedBuffers.OSCCAL = oscCal;
				Pk2.WriteOSSCAL();
				Pk2.DeviceBuffers.ProgramMemory[Pk2.DeviceBuffers.ProgramMemory.Length - 1] =
							Pk2.DeviceBuffers.OSCCAL;
				savedBuffers.ProgramMemory[savedBuffers.ProgramMemory.Length - 1] =
							Pk2.DeviceBuffers.OSCCAL;
			}

			// restore BandGap if need be
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
			{
				Pk2.DeviceBuffers.BandGap = bandGap;
				savedBuffers.BandGap = bandGap;
				Pk2.WriteConfigOutsideProgMem(false, false);
			}

			// write "erased" config words for parts that don't bulk erase configs (ex 18F6520)
			// Also do this for some parts to match MPLAB (ie PIC18J)
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].WriteCfgOnErase)
			{

				// compute configration information.
				int configLocation = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigAddr /
					Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemHexBytes;
				int configWords = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords;
				int endOfBuffer = Pk2.DeviceBuffers.ProgramMemory.Length;
				if ((configLocation < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem) && (configWords > 0))
				{// if config in program memory, set them to clear.
					uint orMask = 0;
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue == 0xFFFF)
					{ // PIC18J
						orMask = 0xF000;
					}
					else
					{ // PIC24FJ
						orMask = 0xFF0000;
					}
					for (int cfg = configWords; cfg > 0; cfg--)
					{
						Pk2.DeviceBuffers.ProgramMemory[endOfBuffer - cfg] =
									Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigBlank[configWords - cfg] | orMask;
					}
					writeConfigInsideProgramMem();
				}
				else
				{
					Pk2.WriteConfigOutsideProgMem(false, false);
				}
			}

			if (!toolStripMenuItemClearBuffersErase.Checked)
			{// restore buffers
				Pk2.DeviceBuffers = savedBuffers;
			}

			displayStatusWindow.Text += "Complete";
			//displayStatusWindow.Update();
			this.Update();
			if (toolStripMenuItemClearBuffersErase.Checked)
			{
				displayDataSource.Text = "None (Empty/Erased)";
			}
			checkImportFile = false;

			conditionalVDDOff();

			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private bool checkEraseVoltage(bool checkRowErase)
		{
			if (((float)(numUpDnVDD.Value + 0.05M) < Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase)
								 && ShowWriteEraseVDDDialog)
			{
				if (checkRowErase && (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript > 0))
				{// if row erase script exists
					return false;       // voltage doesn't support row erase but don't show dialog.
				}
				dialogVddErase.UpdateText();
				bool timerEnabled = timerAutoImportWrite.Enabled;
				timerAutoImportWrite.Enabled = false;
				dialogVddErase.ShowDialog();
				timerAutoImportWrite.Enabled = timerEnabled;
				return ContinueWriteErase;
			}
			return true;
		}


		private bool verifyOSCCAL()
		{

			if (!Pk2.ValidateOSSCAL() && verifyOSCCALValue)
			{
				if (MessageBox.Show
					("Invalid OSCCAL Value detected:\n\nTo abort, click 'Cancel'\nTo continue, click 'OK'",
					 "Warning!", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
				{
					conditionalVDDOff();
					displayStatusWindow.Text = "Operation Aborted.\n";
					statusWindowColor = Constants.StatusColor.red;
					updateGUI(KONST.UpdateMemoryDisplays);
					return false;
				}

			}
			return true;
		}

		/**********************************************************************************************************
		 **********************************************************************************************************
		 ***                                                                                                    ***
		 ***                                          WRITE DEVICE                                              ***
		 ***                                                                                                    *** 
		 ********************************************************************************************************** 
		 **********************************************************************************************************/

		private void writeDevice(object sender, EventArgs e)
		{
			deviceWrite();
		}

		private bool deviceWrite()
		{
			uint checksumPk2Go = 0;

			// These are used when programming the configuration word.
			int configLocation;
			int configWords;
			int endOfBuffer;
			//uint[] backupConfigWords;

			if (Pk2.FamilyIsEEPROM())
			{
				return eepromWrite(KONST.WriteEE);
			}

			bool useLowVoltageRowErase = false;

			if (!preProgrammingCheck(Pk2.GetActiveFamily()))
			{
				return false; // abort
			}

			if (!checkEraseVoltage(true) && !Pk2.FamilyIsPIC32())
			{
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript > 0)
				{ // if device supports row erases, use them
					useLowVoltageRowErase = true;
				}
				else
				{
					return false; // abort
				}
			}

			updateGUI(KONST.DontUpdateMemDisplays);
			this.Update();

			if (checkImportFile && !Pk2.LearnMode)
			{
				FileInfo hexFile = new FileInfo(openHexFileDialog.FileName);
				if (ImportExportHex.LastWriteTime != hexFile.LastWriteTime)
				{
					displayStatusWindow.Text = "Reloading Hex File\n";
					//displayStatusWindow.Update();
					this.Update();
					Thread.Sleep(300);
					if (!importHexFileGo())
					{
						displayStatusWindow.Text = "Error Loading Hex File: Write aborted.\n";
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
			}

			if (Pk2.FamilyIsPIC32())
			{
				if (P32.P32Write(verifyOnWriteToolStripMenuItem.Checked, enableCodeProtectToolStripMenuItem.Checked))
				{
					statusWindowColor = Constants.StatusColor.green;
					conditionalVDDOff();
					updateGUI(KONST.UpdateMemoryDisplays);
					return true;
				}
				else
				{
					statusWindowColor = Constants.StatusColor.red;
					conditionalVDDOff();
					updateGUI(KONST.UpdateMemoryDisplays);
					return true;
				}
			}

			Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
			Pk2.VddOn();
			//Thread.Sleep(100);

			// check device ID in LearnMode
			if (Pk2.LearnMode && (Pk2.DevFile.Families[Pk2.GetActiveFamily()].DeviceIDMask > 0))
			{
				Pk2.MetaCmd_CHECK_DEVICE_ID();
			}

			// Get OSCCAL if need be
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
			{
				if (Pk2.LearnMode)
				{
					// leave OSCCAL blank so we can rewrite it later.
					Pk2.DeviceBuffers.ProgramMemory[Pk2.DeviceBuffers.ProgramMemory.Length - 1] =
						Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
					// Send meta command
					Pk2.MetaCmd_READ_OSCCAL();
				}
				else
				{ // normal operation
					Pk2.ReadOSSCAL();
					// put OSCCAL into part memory so it doesn't have to be written seperately.
					Pk2.DeviceBuffers.ProgramMemory[Pk2.DeviceBuffers.ProgramMemory.Length - 1] =
						Pk2.DeviceBuffers.OSCCAL;

					// verify OSCCAL
					if (!verifyOSCCAL())
					{
						return false;
					}

				}
			}
			uint oscCal = Pk2.DeviceBuffers.OSCCAL;

			// Get BandGap if need be
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask > 0)
			{
				if (Pk2.LearnMode)
				{
					Pk2.MetaCmd_READ_BANDGAP();
				}
				else
				{
					Pk2.ReadBandGap();
				}
			}
			uint bandGap = Pk2.DeviceBuffers.BandGap;

			// Erase Device First
			bool reWriteEE = false;
			if (checkBoxProgMemEnabled.Checked && (checkBoxEEMem.Checked || !checkBoxEEMem.Enabled))
			{ // chip erase when programming all
				if (useLowVoltageRowErase)
				{ // use row erases
					displayStatusWindow.Text = "Erasing Part with Low Voltage Row Erase...\n";
					this.Update();
					Pk2.RowEraseDevice();
				}
				else
				{ // bulk erase
					// dsPIC30F5011, 5013 need configs cleared before erase
					// but don't run this script if a row erase is defined
					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMemEraseScript > 0)
						&& (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseSize == 0))
					{
						Pk2.RunScript(KONST.PROG_ENTRY, 1);
						if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWrPrepScript > 0)
						{
							Pk2.DownloadAddress3(0);
							Pk2.RunScript(KONST.CONFIG_WR_PREP, 1);
						}
						Pk2.ExecuteScript(Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMemEraseScript);
						Pk2.RunScript(KONST.PROG_EXIT, 1);
					}

					Pk2.RunScript(KONST.PROG_ENTRY, 1);
					if (Pk2.DevFile.PartsList[Pk2.ActivePart].ChipErasePrepScript > 0)
					{
						Pk2.RunScript(KONST.ERASE_CHIP_PREP, 1);
					}

					Pk2.RunScript(KONST.ERASE_CHIP, 1);
					Pk2.RunScript(KONST.PROG_EXIT, 1);
				}
			}
			else if (checkBoxProgMemEnabled.Checked
				&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemEraseScript != 0))
			{ // Don't erase EE when not selected.
				if (useLowVoltageRowErase)
				{ // use row erases
					displayStatusWindow.Text = "Erasing Part with Low Voltage Row Erase...\n";
					this.Update();
					Pk2.RowEraseDevice();
				}
				else
				{ // bulk erases
					Pk2.RunScript(KONST.PROG_ENTRY, 1);
					Pk2.RunScript(KONST.ERASE_PROGMEM, 1);
					Pk2.RunScript(KONST.PROG_EXIT, 1);
				}
			}
			else if (checkBoxEEMem.Checked
				&& (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMemEraseScript != 0))
			{ // Some parts must have EE bulk erased before being re-written.
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				Pk2.RunScript(KONST.ERASE_EE, 1);
				Pk2.RunScript(KONST.PROG_EXIT, 1);
			}
			else if ((!checkBoxEEMem.Checked && checkBoxEEMem.Enabled)
				&& Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemEraseScript == 0)
			{// Some parts cannot erase ProgMem, UserID, & config without erasing EE
				// so must read & re-write EE.
				displayStatusWindow.Text = "Reading device:\n";
				this.Update();
				readEEPROM();
				updateGUI(true);
				if (useLowVoltageRowErase)
				{ // use row erases
					displayStatusWindow.Text = "Erasing Part with Low Voltage Row Erase...\n";
					this.Update();
					Pk2.RowEraseDevice();
				}
				else
				{
					// bulk erase
					// dsPIC30F5011, 5013 need configs cleared before erase
					// but don't run this script if a row erase is defined
					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMemEraseScript > 0)
						&& (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseSize == 0))
					{
						Pk2.RunScript(KONST.PROG_ENTRY, 1);
						if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWrPrepScript > 0)
						{
							Pk2.DownloadAddress3(0);
							Pk2.RunScript(KONST.CONFIG_WR_PREP, 1);
						}
						Pk2.ExecuteScript(Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMemEraseScript);
						Pk2.RunScript(KONST.PROG_EXIT, 1);
					}
					Pk2.RunScript(KONST.PROG_ENTRY, 1);
					if (Pk2.DevFile.PartsList[Pk2.ActivePart].ChipErasePrepScript > 0)
					{
						Pk2.RunScript(KONST.ERASE_CHIP_PREP, 1);
					}
					Pk2.RunScript(KONST.ERASE_CHIP, 1);
					Pk2.RunScript(KONST.PROG_EXIT, 1);
					reWriteEE = true;
				}
			}

			displayStatusWindow.Text = "Writing device:\n";
			//displayStatusWindow.Update();
			this.Update();

			bool configInProgramSpace = false;

			// compute configration information.
			configLocation = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigAddr /
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemHexBytes;
			configWords = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords;
			endOfBuffer = Pk2.DeviceBuffers.ProgramMemory.Length;
			uint[] configBackups = new uint[configWords];  // use because DevicBuffers are masked & won't verify later
			if ((configLocation < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem) && (configWords > 0))
			{// if config in program memory, set them to clear.
				configInProgramSpace = true;
				for (int cfg = configWords; cfg > 0; cfg--)
				{
					configBackups[cfg - 1] = Pk2.DeviceBuffers.ProgramMemory[endOfBuffer - cfg];
					Pk2.DeviceBuffers.ProgramMemory[endOfBuffer - cfg] =
								Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
				}
			}
			endOfBuffer--;

			// Write Program Memory
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].PartName == "PIC12F529")
			{
				// The PIC12F529 requires code protection to be disabled whenever the
				// configuration word is erased (the erased state of the config word 
				// does not disable code protect). This is taken care of the "Write
				// Config On Erase" flag when erasing the device manually. Here, since
				// that flag is not checked, we will detect the device by name and 
				// force a write of the config word. 

				Pk2.Unlock529Config(false, false);
			}

			if (checkBoxProgMemEnabled.Checked)
			{
				displayStatusWindow.Text += "Program Memory... ";
				//displayStatusWindow.Update();
				this.Update();
				progressBar1.Value = 0;     // reset bar

				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrPrepScript != 0)
				{ // if prog mem address set script exists for this part
					Pk2.DownloadAddress3(0);
					//Pk2.DownloadAddress3(0x200000);
					Pk2.RunScript(KONST.PROGMEM_WR_PREP, 1);
				}
				if (Pk2.FamilyIsKeeloq())
				{
					Pk2.HCS360_361_VppSpecial();
				}

				int wordsPerWrite = Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrWords;
				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation;
				int scriptRunsToUseDownload = KONST.DownLoadBufferSize /
					(wordsPerWrite * bytesPerWord);
				int wordsPerLoop = scriptRunsToUseDownload * wordsPerWrite;
				int wordsWritten = 0;

				// Find end of used memory
				endOfBuffer = Pk2.FindLastUsedInBuffer(Pk2.DeviceBuffers.ProgramMemory,
											Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue, endOfBuffer);
				if (((wordsPerWrite == (endOfBuffer + 1)) || (wordsPerLoop > (endOfBuffer + 1))) && !Pk2.LearnMode)
				{ // very small memory sizes (like HCS parts)
					scriptRunsToUseDownload = 1;
					wordsPerLoop = wordsPerWrite;
				}
				// align end on next loop boundary                 
				int writes = (endOfBuffer + 1) / wordsPerLoop;
				if (((endOfBuffer + 1) % wordsPerLoop) > 0)
				{
					writes++;
				}
				endOfBuffer = writes * wordsPerLoop;

				progressBar1.Maximum = (int)endOfBuffer / wordsPerLoop;

				if (useProgExec33())
				{
					if (!PE33.PE33Write(endOfBuffer, displayStatusWindow.Text))
					{
						statusWindowColor = Constants.StatusColor.red;
						conditionalVDDOff();
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
				else if (useProgExec24F())
				{
					if (!PE24.PE24FWrite(endOfBuffer, displayStatusWindow.Text, verifyOnWriteToolStripMenuItem.Checked))
					{
						statusWindowColor = Constants.StatusColor.red;
						conditionalVDDOff();
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
				else
				{
					byte[] downloadBuffer = new byte[KONST.DownLoadBufferSize];

					do
					{
						int downloadIndex = 0;
						for (int word = 0; word < wordsPerLoop; word++)
						{
							if (wordsWritten == endOfBuffer)
							{
								break; // for cases where ProgramMemSize%WordsPerLoop != 0
							}
							uint memWord = Pk2.DeviceBuffers.ProgramMemory[wordsWritten++];
							if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
							{
								memWord = memWord << 1;
							}

							downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
							checksumPk2Go += (byte)(memWord & 0xFF);

							for (int bite = 1; bite < bytesPerWord; bite++)
							{
								memWord >>= 8;
								downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
								checksumPk2Go += (byte)(memWord & 0xFF);
							}

						}
						// download data
						if (Pk2.FamilyIsKeeloq())
						{
							processKeeloqData(ref downloadBuffer, wordsWritten);
						}
						int dataIndex = Pk2.DataClrAndDownload(downloadBuffer, 0);
						while (dataIndex < downloadIndex)
						{
							dataIndex = Pk2.DataDownload(downloadBuffer, dataIndex, downloadIndex);
						}

						Pk2.RunScript(KONST.PROGMEM_WR, scriptRunsToUseDownload);

						if (((wordsWritten % 0x8000) == 0) && (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrPrepScript != 0))
						{ //PIC24 must update TBLPAG
							Pk2.DownloadAddress3(0x10000 * (wordsWritten / 0x8000));
							Pk2.RunScript(KONST.PROGMEM_WR_PREP, 1);
						}

						progressBar1.PerformStep();
					} while (wordsWritten < endOfBuffer);

					Pk2.RunScript(KONST.PROG_EXIT, 1);
				}
			}

			int verifyStop = endOfBuffer;

			if (configInProgramSpace)
			{// if config in program memory, restore prog memory to proper values.
				for (int cfg = configWords; cfg > 0; cfg--)
				{
					Pk2.DeviceBuffers.ProgramMemory[Pk2.DeviceBuffers.ProgramMemory.Length - cfg]
							= configBackups[cfg - 1];
				}
			}

			// Write EEPROM
			if (((checkBoxEEMem.Checked) || reWriteEE) && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
			{
				displayStatusWindow.Text += "EE... ";
				this.Update();

				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				if (Pk2.DevFile.PartsList[Pk2.ActivePart].EEWrPrepScript > 1)
				{
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemHexBytes == 4)
					{ // 16-bit parts
						Pk2.DownloadAddress3((int)(Pk2.DevFile.PartsList[Pk2.ActivePart].EEAddr / 2));
					}
					else
					{
						Pk2.DownloadAddress3(0);
					}
					Pk2.RunScript(KONST.EE_WR_PREP, 1);
				}

				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemBytesPerWord;
				uint eeBlank = getEEBlank();

				// write at least 16 locations per loop
				int locationsPerLoop = Pk2.DevFile.PartsList[Pk2.ActivePart].EEWrLocations;
				if (locationsPerLoop < 16)
				{
					locationsPerLoop = 16;
				}

				// find end of used EE
				if (checkBoxProgMemEnabled.Checked && !useLowVoltageRowErase && !Pk2.LearnMode)
				{ // we're writing all, so EE is erased first, we can skip blank locations at end
					// unless we're using LVRowErase in which we need to write all as EE isn't erased first.
					endOfBuffer = Pk2.FindLastUsedInBuffer(Pk2.DeviceBuffers.EEPromMemory, eeBlank,
											Pk2.DeviceBuffers.EEPromMemory.Length - 1);
				}
				else
				{ // if we're only writing EE, must write blanks in case they aren't blank on device
					endOfBuffer = Pk2.DeviceBuffers.EEPromMemory.Length - 1;
				}
				// align end on next loop boundary                 
				int writes = (endOfBuffer + 1) / locationsPerLoop;
				if (((endOfBuffer + 1) % locationsPerLoop) > 0)
				{
					writes++;
				}
				endOfBuffer = writes * locationsPerLoop;


				byte[] downloadBuffer = new byte[(locationsPerLoop * bytesPerWord)];

				int scriptRunsPerLoop = locationsPerLoop / Pk2.DevFile.PartsList[Pk2.ActivePart].EEWrLocations;
				int locationsWritten = 0;

				progressBar1.Value = 0;     // reset bar
				progressBar1.Maximum = (int)endOfBuffer / locationsPerLoop;
				do
				{
					int downloadIndex = 0;
					for (int word = 0; word < locationsPerLoop; word++)
					{
						uint eeWord = Pk2.DeviceBuffers.EEPromMemory[locationsWritten++];
						if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
						{
							eeWord = eeWord << 1;
						}

						downloadBuffer[downloadIndex++] = (byte)(eeWord & 0xFF);
						checksumPk2Go += (byte)(eeWord & 0xFF);

						for (int bite = 1; bite < bytesPerWord; bite++)
						{
							eeWord >>= 8;
							downloadBuffer[downloadIndex++] = (byte)(eeWord & 0xFF);
							checksumPk2Go += (byte)(eeWord & 0xFF);
						}
					}
					// download data
					Pk2.DataClrAndDownload(downloadBuffer, 0);
					Pk2.RunScript(KONST.EE_WR, scriptRunsPerLoop);

					progressBar1.PerformStep();
				} while (locationsWritten < endOfBuffer);
				Pk2.RunScript(KONST.PROG_EXIT, 1);
			}

			// Write UserIDs
			if (checkBoxProgMemEnabled.Checked && (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords > 0))
			{ // do not write if EE unselected as PIC18F cannot erase/write UserIDs except with ChipErase
				displayStatusWindow.Text += "UserIDs... ";
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				if (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWrPrepScript > 0)
				{
					Pk2.RunScript(KONST.USERID_WR_PREP, 1);
				}

				int bytesPerID = Pk2.DevFile.Families[Pk2.GetActiveFamily()].UserIDBytes;
				byte[] downloadBuffer = new byte[Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords * bytesPerID];

				int downloadIndex = 0;
				int idWritten = 0;
				for (int word = 0; word < Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords; word++)
				{
					uint memWord = Pk2.DeviceBuffers.UserIDs[idWritten++];
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						memWord = memWord << 1;
					}

					downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
					checksumPk2Go += (byte)(memWord & 0xFF);

					for (int bite = 1; bite < bytesPerID; bite++)
					{
						memWord >>= 8;
						downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
						checksumPk2Go += (byte)(memWord & 0xFF);
					}

				}
				// download data
				int dataIndex = Pk2.DataClrAndDownload(downloadBuffer, 0);
				while (dataIndex < downloadIndex)
				{
					dataIndex = Pk2.DataDownload(downloadBuffer, dataIndex, downloadIndex);
				}

				Pk2.RunScript(KONST.USERID_WR, 1);

				Pk2.RunScript(KONST.PROG_EXIT, 1);
			}

			bool verifySuccess = true;

			// Verify all but config (since hasn't been written as may contain code protection settings.
			if (configInProgramSpace)
			{// if config in program memory, don't verify configs.
				if (Pk2.LearnMode)
				{
					if (verifyStop == Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem)
					{
						// last block of program memory where config words are
						if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFF)
						{ // 24FJ
							checksumPk2Go -= 0x80;  // adjust for "7F" in blank configs.
						}
						else
						{ // 18J
							checksumPk2Go -= 0x08;  // adjust for "F7" in blank configs.
						}
					}
				}
				else
				{ // normal
					verifyStop = verifyStop - configWords;
				}
			}


			if (verifyOnWriteToolStripMenuItem.Checked)
			{
				if (Pk2.LearnMode)
				{
					Pk2.MetaCmd_START_CHECKSUM();
				}

				verifySuccess = deviceVerify(true, (verifyStop - 1), reWriteEE);

				if (Pk2.LearnMode)
				{
					Pk2.MetaCmd_VERIFY_CHECKSUM(checksumPk2Go);
					checksumPk2Go = 0; // clear for config check
				}
			}

			if (Pk2.LearnMode && Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
			{
				Pk2.MetaCmd_WRITE_OSCCAL();
				// restore OSCCAL
				Pk2.DeviceBuffers.ProgramMemory[Pk2.DeviceBuffers.ProgramMemory.Length - 1] =
						Pk2.DeviceBuffers.OSCCAL;
			}

			// WRITE CONFIGURATION
			if (verifySuccess)
			{ // if we've failed verification, don't try to finish write
				// Write Configuration
				if ((configWords > 0) && (!configInProgramSpace) && checkBoxProgMemEnabled.Checked)
				{ // Write config words differently for any part where they are stored in program memory.
					if (!verifyOnWriteToolStripMenuItem.Checked)
					{
						displayStatusWindow.Text += "Config... ";
						//displayStatusWindow.Update();
						this.Update();
					}

					// 18F devices create a problem as the WRTC bit in the next to last config word
					// is effective immediately upon being written, which if asserted prevents the 
					// last config word from being written.
					// To get around this, we're using a bit of hack.  Detect PIC18F or PIC18F_K_parts,
					// and look for WRTC = 0.  If found, write config words once with CONFIG6 = 0xFFFF
					// then re-write it with the correct value.
					if ((Pk2.DevFile.Families[Pk2.GetActiveFamily()].FamilyName == "PIC18F") ||
						(Pk2.DevFile.Families[Pk2.GetActiveFamily()].FamilyName == "PIC18F_K_"))
					{
						if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords > 5)
						{ // don't blow up if part doesn't have enough config words
							if ((Pk2.DeviceBuffers.ConfigWords[5] & ~0x2000) == Pk2.DeviceBuffers.ConfigWords[5])
							{ // if WRTC is asserted
								uint saveConfig6 = Pk2.DeviceBuffers.ConfigWords[5];
								Pk2.DeviceBuffers.ConfigWords[5] = 0xFFFF;
								Pk2.WriteConfigOutsideProgMem(false, false); // no protects
								Pk2.DeviceBuffers.ConfigWords[5] = saveConfig6;
							}

						}
					}
					checksumPk2Go += Pk2.WriteConfigOutsideProgMem((enableCodeProtectToolStripMenuItem.Enabled && enableCodeProtectToolStripMenuItem.Checked),
												  (enableDataProtectStripMenuItem.Enabled && enableDataProtectStripMenuItem.Checked));

					//Adjust for some PIC18F masked config bits that remain set.
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue == 0xFFFF)
					{
						checksumPk2Go += Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[7];
					}

					// Verify Configuration
					if (verifyOnWriteToolStripMenuItem.Checked)
					{
						if (!Pk2.LearnMode || (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask == 0))
						{
							bool verifyGood = verifyConfig(configWords, configLocation);
							if (verifyGood)
							{
								statusWindowColor = Constants.StatusColor.green;
								displayStatusWindow.Text = "Programming Successful.\n";
							}
							else if (!Pk2.LearnMode)
							{
								statusWindowColor = Constants.StatusColor.red;
								verifySuccess = false;
							}
							if (Pk2.LearnMode && (Pk2.DevFile.PartsList[Pk2.ActivePart].BandGapMask == 0))
							{
								Pk2.MetaCmd_VERIFY_CHECKSUM(checksumPk2Go);
							}
						}
					}
				}
				else if ((configWords > 0) && checkBoxProgMemEnabled.Checked)
				{  // for parts where config resides in program memory.
					// program last memory block.
					if (!verifyOnWriteToolStripMenuItem.Checked)
					{
						displayStatusWindow.Text += "Config... ";
						//displayStatusWindow.Update();               
						this.Update();
					}
					for (int i = 0; i < configWords; i++)
					{ // Set code protects & blank masks
						if (i == Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1)
						{
							if (enableCodeProtectToolStripMenuItem.Enabled && enableCodeProtectToolStripMenuItem.Checked)
							{
								Pk2.DeviceBuffers.ProgramMemory[configLocation + i] &=
												(uint)~Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask;
							}
							if (enableDataProtectStripMenuItem.Enabled && enableDataProtectStripMenuItem.Checked)
							{
								Pk2.DeviceBuffers.ProgramMemory[configLocation + i] &=
												(uint)~Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask;
							}
						}
					}

					writeConfigInsideProgramMem();

					if (verifyOnWriteToolStripMenuItem.Checked)
					{
						statusWindowColor = Constants.StatusColor.green;
						displayStatusWindow.Text = "Programming Successful.\n";
					}
					else
					{
						verifySuccess = false;
					}

				}
				else if (!checkBoxProgMemEnabled.Checked)
				{
					statusWindowColor = Constants.StatusColor.green;
					displayStatusWindow.Text = "Programming Successful.\n";
				}
				else
				{ // HCS parts
					statusWindowColor = Constants.StatusColor.green;
					displayStatusWindow.Text = "Programming Successful.\n";
				}

				// TEST for DEBUG vector write
				//Pk2.WriteDebugVector(0x12343ABC);

				conditionalVDDOff();

				if (!verifyOnWriteToolStripMenuItem.Checked)
				{
					displayStatusWindow.Text += "Done.";
				}
				if (Pk2.LearnMode)
				{
					displayStatusWindow.Text = "Programmer-To-Go download complete.";
				}
				updateGUI(KONST.UpdateMemoryDisplays);

				return verifySuccess;

			}
			return false; // verifySuccess false

		}

		private void writeConfigInsideProgramMem()
		{

			Pk2.RunScript(KONST.PROG_ENTRY, 1);
			int lastBlock = Pk2.DeviceBuffers.ProgramMemory.Length -
							Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrWords;
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrPrepScript != 0)
			{ // if prog mem address set script exists for this part
				Pk2.DownloadAddress3(lastBlock * Pk2.DevFile.Families[Pk2.GetActiveFamily()].AddressIncrement);
				Pk2.RunScript(KONST.PROGMEM_WR_PREP, 1);
			}
			byte[] downloadBuffer = new byte[KONST.DownLoadBufferSize];
			int downloadIndex = 0;
			for (int word = 0; word < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrWords; word++)
			{
				uint memWord = Pk2.DeviceBuffers.ProgramMemory[lastBlock++];
				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
				{
					memWord = memWord << 1;
				}
				downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
				for (int bite = 1; bite < Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation; bite++)
				{
					memWord >>= 8;
					downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
				}

			}
			// download data
			int dataIndex = Pk2.DataClrAndDownload(downloadBuffer, 0);
			while (dataIndex < downloadIndex)
			{
				dataIndex = Pk2.DataDownload(downloadBuffer, dataIndex, downloadIndex);
			}

			Pk2.RunScript(KONST.PROGMEM_WR, 1);
			Pk2.RunScript(KONST.PROG_EXIT, 1);
		}

		private void processKeeloqData(ref byte[] downloadBuffer, int wordsWritten)
		{
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].DeviceID == 0xFFFFFF36)
			{ // do nothing unless it's the HCS360 or 361
				for (int i = (wordsWritten / 2); i > 0; i--)
				{
					downloadBuffer[i * 4 - 1] = (byte)~downloadBuffer[i * 2 - 1];
					downloadBuffer[i * 4 - 2] = (byte)~downloadBuffer[i * 2 - 2];
					downloadBuffer[i * 4 - 3] = downloadBuffer[i * 2 - 1];   // 360,361 need complements
					downloadBuffer[i * 4 - 4] = downloadBuffer[i * 2 - 2];
				}
				downloadBuffer[0] >>= 1; // first buffer should only contain 7MSBs of byte.
			}
		}


		/**********************************************************************************************************
		 **********************************************************************************************************
		 ***                                                                                                    ***
		 ***                                           BLANK CHECK                                              ***
		 ***                                                                                                    *** 
		 ********************************************************************************************************** 
		 **********************************************************************************************************/

		private void blankCheck(object sender, EventArgs e)
		{
			blankCheckDevice();
		}

		private bool blankCheckDevice()
		{
			if (Pk2.FamilyIsKeeloq() || Pk2.FamilyIsMCP())
			{
				displayStatusWindow.Text = "Blank Check not supported for this device type.";
				statusWindowColor = Constants.StatusColor.yellow;
				updateGUI(KONST.DontUpdateMemDisplays);
				return false; // abort
			}

			if (!preProgrammingCheck(Pk2.GetActiveFamily()))
			{
				return false; // abort
			}

			if (Pk2.FamilyIsPIC32())
			{
				if (P32.PIC32BlankCheck())
				{
					statusWindowColor = Constants.StatusColor.green;
					conditionalVDDOff();
					updateGUI(KONST.UpdateMemoryDisplays);
					return true;
				}
				else
				{
					statusWindowColor = Constants.StatusColor.red;
					conditionalVDDOff();
					updateGUI(KONST.UpdateMemoryDisplays);
					return true;
				}
			}

			DeviceData blankDevice = new DeviceData(Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem,
				Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem,
				Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords,
				Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords,
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue,
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemAddressIncrement,
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].UserIDBytes,
				Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigBlank,
				Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.OSCCAL_MASK]);

			// handle situation where configs are in program memory.
			int configLocation = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigAddr /
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemHexBytes;
			int configWords = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords;
			if (configLocation < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem)
				for (int i = 0; i < configWords; i++)
				{
					uint template = blankDevice.ProgramMemory[configLocation + i] & 0xFFFF0000;
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue == 0xFFFF)
					{
						template |= 0xF000;
					}
					blankDevice.ProgramMemory[configLocation + i] =
							(template | Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigBlank[i]);
				}

			displayStatusWindow.Text = "Checking if Device is blank:\n";
			//displayStatusWindow.Update();
			this.Update();

			Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
			Pk2.VddOn();

			byte[] upload_buffer = new byte[KONST.UploadBufferSize];

			//Check Program Memory ----------------------------------------------------------------------------
			displayStatusWindow.Text += "Program Memory... ";
			//displayStatusWindow.Update();
			this.Update();

			if (useProgExec33())
			{
				if (!PE33.PE33BlankCheck(displayStatusWindow.Text))
				{
					conditionalVDDOff();
					displayStatusWindow.Text = "Program Memory is not blank.";
					statusWindowColor = Constants.StatusColor.red;
					updateGUI(KONST.UpdateMemoryDisplays);
					return false;
				}
			}
			else if (useProgExec24F())
			{
				if (!PE24.PE24FBlankCheck(displayStatusWindow.Text))
				{
					conditionalVDDOff();
					statusWindowColor = Constants.StatusColor.red;
					updateGUI(KONST.UpdateMemoryDisplays);
					return false;
				}
			}
			else
			{
				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrSetScript != 0)
						&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrBytes != 0))
				{ // if prog mem address set script exists for this part
					if (Pk2.FamilyIsEEPROM())
					{
						Pk2.DownloadAddress3MSBFirst(eeprom24BitAddress(0, KONST.WRITE_BIT));
						Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
						if (eeprom_CheckBusErrors())
						{
							return false;
						}
					}
					else
					{
						Pk2.DownloadAddress3(0);
						Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
					}
				}

				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation;
				int scriptRunsToFillUpload = KONST.UploadBufferSize /
					(Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords * bytesPerWord);
				int wordsPerLoop = scriptRunsToFillUpload * Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords;
				int wordsRead = 0;

				progressBar1.Value = 0;     // reset bar
				progressBar1.Maximum = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem / wordsPerLoop;

				do
				{
					if (Pk2.FamilyIsEEPROM())
					{
						if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
							&& (wordsRead > Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG])
							&& (wordsRead % (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] + 1) == 0))
						{
							Pk2.DownloadAddress3MSBFirst(eeprom24BitAddress(wordsRead, KONST.WRITE_BIT));
							Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
						}
						Pk2.Download3Multiples(eeprom24BitAddress(wordsRead, KONST.READ_BIT), scriptRunsToFillUpload,
									Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords);
					}
					//Pk2.RunScriptUploadNoLen2(KONST.PROGMEM_RD, scriptRunsToFillUpload);
					Pk2.RunScriptUploadNoLen(KONST.PROGMEM_RD, scriptRunsToFillUpload);
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
					//Pk2.GetUpload();
					Pk2.UploadDataNoLen();
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
					int uploadIndex = 0;
					for (int word = 0; word < wordsPerLoop; word++)
					{
						int bite = 0;
						uint memWord = (uint)upload_buffer[uploadIndex + bite++];
						if (bite < bytesPerWord)
						{
							memWord |= (uint)upload_buffer[uploadIndex + bite++] << 8;
						}
						if (bite < bytesPerWord)
						{
							memWord |= (uint)upload_buffer[uploadIndex + bite++] << 16;
						}
						if (bite < bytesPerWord)
						{
							memWord |= (uint)upload_buffer[uploadIndex + bite++] << 24;
						}
						uploadIndex += bite;
						// shift if necessary
						if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
						{
							memWord = (memWord >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
						}

						// if OSCCAL save, force last word to be blank
						if ((Pk2.DevFile.PartsList[Pk2.ActivePart].OSSCALSave)
								&& wordsRead == (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem - 1))
						{
							memWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
						}
						if (memWord != blankDevice.ProgramMemory[wordsRead++])
						{
							Pk2.RunScript(KONST.PROG_EXIT, 1);
							conditionalVDDOff();
							if (Pk2.FamilyIsEEPROM())
							{
								displayStatusWindow.Text = "EEPROM is not blank starting at address\n";
							}
							else
							{
								displayStatusWindow.Text = "Program Memory is not blank starting at address\n";
							}
							displayStatusWindow.Text += string.Format("0x{0:X6}",
								(--wordsRead * Pk2.DevFile.Families[Pk2.GetActiveFamily()].AddressIncrement));
							statusWindowColor = Constants.StatusColor.red;
							updateGUI(KONST.UpdateMemoryDisplays);
							return false;
						}

						if (wordsRead == Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem)
						{
							break; // for cases where ProgramMemSize%WordsPerLoop != 0
						}
						if (((wordsRead % 0x8000) == 0)
								&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrSetScript != 0)
								&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrBytes != 0)
								&& (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFF))
						{ //PIC24 must update TBLPAG
							Pk2.DownloadAddress3(0x10000 * (wordsRead / 0x8000));
							Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
							break;
						}
					}
					progressBar1.PerformStep();
				} while (wordsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem);
				Pk2.RunScript(KONST.PROG_EXIT, 1);
			}


			//Check EEPROM ------------------------------------------------------------------------------------
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0)
			{
				displayStatusWindow.Text += "EE... ";
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				if (Pk2.DevFile.PartsList[Pk2.ActivePart].EERdPrepScript > 0)
				{
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemHexBytes == 4)
					{ // 16-bit parts
						Pk2.DownloadAddress3((int)(Pk2.DevFile.PartsList[Pk2.ActivePart].EEAddr / 2));
					}
					else
					{
						Pk2.DownloadAddress3(0);
					}
					Pk2.RunScript(KONST.EE_RD_PREP, 1);
				}

				int bytesPerLoc = Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemBytesPerWord;
				uint eeBlank = getEEBlank();
				int scriptRuns2FillUpload = KONST.UploadBufferSize /
					(Pk2.DevFile.PartsList[Pk2.ActivePart].EERdLocations * bytesPerLoc);
				int locPerLoop = scriptRuns2FillUpload * Pk2.DevFile.PartsList[Pk2.ActivePart].EERdLocations;
				int locsRead = 0;

				progressBar1.Value = 0;     // reset bar
				progressBar1.Maximum = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem / locPerLoop;
				do
				{
					//Pk2.RunScriptUploadNoLen2(KONST.EE_RD, scriptRuns2FillUpload);
					Pk2.RunScriptUploadNoLen(KONST.EE_RD, scriptRuns2FillUpload);
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
					//Pk2.GetUpload();
					Pk2.UploadDataNoLen();
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
					int uploadIndex = 0;
					for (int word = 0; word < locPerLoop; word++)
					{
						int bite = 0;
						uint memWord = (uint)upload_buffer[uploadIndex + bite++];
						if (bite < bytesPerLoc)
						{
							memWord |= (uint)upload_buffer[uploadIndex + bite++] << 8;
						}
						uploadIndex += bite;
						// shift if necessary
						if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
						{
							memWord = (memWord >> 1) & eeBlank;
						}
						locsRead++;
						if (memWord != eeBlank)
						{
							Pk2.RunScript(KONST.PROG_EXIT, 1);
							conditionalVDDOff();
							displayStatusWindow.Text = "EE Data Memory is not blank starting at address\n";
							if (eeBlank == 0xFFFF)
							{
								displayStatusWindow.Text += string.Format("0x{0:X4}", (--locsRead * 2));
							}
							else
							{
								displayStatusWindow.Text += string.Format("0x{0:X4}", --locsRead);
							}
							statusWindowColor = Constants.StatusColor.red;
							updateGUI(KONST.UpdateMemoryDisplays);
							return false;
						}
						if (locsRead >= Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem)
						{
							break; // for cases where ProgramMemSize%WordsPerLoop != 0
						}
					}
					progressBar1.PerformStep();
				} while (locsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem);
				Pk2.RunScript(KONST.PROG_EXIT, 1);
			}

			//Check User IDs ----------------------------------------------------------------------------------
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords > 0) &&
				!Pk2.DevFile.PartsList[Pk2.ActivePart].BlankCheckSkipUsrIDs)
			{
				displayStatusWindow.Text += "UserIDs... ";
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDRdPrepScript > 0)
				{
					Pk2.RunScript(KONST.USERID_RD_PREP, 1);
				}
				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].UserIDBytes;
				int wordsRead = 0;
				int bufferIndex = 0;
				Pk2.RunScriptUploadNoLen(KONST.USERID_RD, 1);
				Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
				if ((Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords * bytesPerWord) > KONST.USB_REPORTLENGTH)
				{
					Pk2.UploadDataNoLen();
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
				}
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				do
				{
					int bite = 0;
					uint memWord = (uint)upload_buffer[bufferIndex + bite++];
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 8;
					}
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 16;
					}
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 24;
					}
					bufferIndex += bite;
					// shift if necessary
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						memWord = ((memWord >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue);
					}
					wordsRead++;
					uint blank = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
					if (bytesPerWord == 1)
					{
						blank &= 0xFF;
					}
					if (memWord != blank)
					{
						conditionalVDDOff();
						displayStatusWindow.Text = "User IDs are not blank.";
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				} while (wordsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords);
			}




			// Blank Check Configuration --------------------------------------------------------------------
			if ((configWords > 0) && (configLocation > Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem))
			{ // Don't read config words for any part where they are stored in program memory.
				displayStatusWindow.Text += "Config... ";
				//displayStatusWindow.Update();
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				Pk2.RunScript(KONST.CONFIG_RD, 1);
				Pk2.UploadData();
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				int bufferIndex = 2;                    // report starts on index 1, which is #bytes uploaded.
				for (int word = 0; word < configWords; word++)
				{
					uint config = (uint)Pk2.Usb_read_array[bufferIndex++];
					config |= (uint)Pk2.Usb_read_array[bufferIndex++] << 8;
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						config = (config >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
					}
					config &= Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[word];
					int configBlank = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[word]
									   & Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigBlank[word];

					if (Pk2.DevFile.PartsList[Pk2.ActivePart].PartName == "PIC12F529")
					{
						// The PIC12F529 has "write only" bits in its config word. Mask those
						// bits from the expected value.
						configBlank |= 0x780;
					}

					if (configBlank != config)
					{
						conditionalVDDOff();
						displayStatusWindow.Text = "Configuration is not blank.";
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
			}


			Pk2.RunScript(KONST.PROG_EXIT, 1);
			conditionalVDDOff();

			statusWindowColor = Constants.StatusColor.green;
			displayStatusWindow.Text = "Device is Blank.";

			updateGUI(KONST.UpdateMemoryDisplays);

			return true;
		}



		private void progMemEdit(object sender, DataGridViewCellEventArgs e)
		{
			int row = e.RowIndex;
			int col = e.ColumnIndex;
			string editText = "0x" + dataGridProgramMemory[col, row].FormattedValue.ToString();
			int value = 0;
			try
			{
				value = UTIL.Convert_Value_To_Int(editText);
			}
			catch
			{
				value = 0;
			}
			int numColumns = dataGridProgramMemory.ColumnCount - 1;
			if (comboBoxProgMemView.SelectedIndex >= 1) // ascii view
			{
				numColumns /= 2;
			}

			int index = ((row * numColumns) + col - 1);

			if (Pk2.FamilyIsPIC32())
			{
				int progMemP32 = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem;
				int bootMemP32 = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].BootFlash;
				progMemP32 -= bootMemP32; // boot flash at upper end of prog mem.

				index -= numColumns; // first row has "Program Flash" text

				if (index > progMemP32)
				{
					index -= numColumns; // subtract row with "Boot Flash" text  
				}
			}

			Pk2.DeviceBuffers.ProgramMemory[index] =
						(uint)(value & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue);

			displayDataSource.Text = "Edited.";
			checkImportFile = false;

			progMemJustEdited = true;
			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private void eEpromEdit(object sender, DataGridViewCellEventArgs e)
		{
			int row = e.RowIndex;
			int col = e.ColumnIndex;
			string editText = "0x" + dataGridViewEEPROM[col, row].FormattedValue.ToString();
			int value = 0;
			try
			{
				value = UTIL.Convert_Value_To_Int(editText);
			}
			catch
			{
				value = 0;
			}
			int numColumns = dataGridViewEEPROM.ColumnCount - 1;
			if (comboBoxEE.SelectedIndex >= 1) // ascii view
			{
				numColumns /= 2;
			}

			Pk2.DeviceBuffers.EEPromMemory[((row * numColumns) + col - 1)] = (uint)(value & getEEBlank());

			displayDataSource.Text = "Edited.";
			checkImportFile = false;

			eeMemJustEdited = true;
			updateGUI(KONST.UpdateMemoryDisplays);

		}

		private void checkCommunication(object sender, EventArgs e)
		{
			if (!detectPICkit2(KONST.ShowMessage, true))
			{
				return;

			}

			partialEnableGUIControls();

			lookForPoweredTarget(KONST.NoMessage);

			if (!Pk2.DevFile.Families[Pk2.GetActiveFamily()].PartDetect)
			{
				setGUIVoltageLimits(true);
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);
				displayStatusWindow.Text = displayStatusWindow.Text + "\n[Parts in this family are not auto-detect.]";
				fullEnableGUIControls();
			}
			else if (Pk2.DetectDevice(KONST.SEARCH_ALL_FAMILIES, true, chkBoxVddOn.Checked))
			{
				setGUIVoltageLimits(true);
				Pk2.SetVDDVoltage((float)numUpDnVDD.Value, 0.85F);
				displayStatusWindow.Text = displayStatusWindow.Text + "\nPIC Device Found.";
				fullEnableGUIControls();
			}

			displayDataSource.Text = "None (Empty/Erased)";

			checkForPowerErrors();

			updateGUI(KONST.UpdateMemoryDisplays);
		}

		/**********************************************************************************************************
		 **********************************************************************************************************
		 ***                                                                                                    ***
		 ***                                         VERIFY DEVICE                                              ***
		 ***                                                                                                    *** 
		 ********************************************************************************************************** 
		 **********************************************************************************************************/

		private void verifyDevice(object sender, EventArgs e)
		{
			if (Pk2.FamilyIsKeeloq())
			{
				displayStatusWindow.Text = "Verify not supported for this device type.";
				statusWindowColor = Constants.StatusColor.yellow;
				updateGUI(KONST.DontUpdateMemDisplays);
				return; // abort
			}

			deviceVerify(false, 0, false);
		}

		private bool deviceVerify(bool writeVerify, int lastLocation, bool forceEEVerify)
		{
			if (!writeVerify)
			{ // only check if "stand-alone" verify            
				if (!preProgrammingCheck(Pk2.GetActiveFamily()))
				{
					return false; // abort
				}
			}

			if (Pk2.FamilyIsPIC32())
			{
				if (P32.P32Verify(writeVerify, enableCodeProtectToolStripMenuItem.Checked))
				{
					statusWindowColor = Constants.StatusColor.green;
					conditionalVDDOff();
					updateGUI(KONST.UpdateMemoryDisplays);
					return true;
				}
				else
				{
					statusWindowColor = Constants.StatusColor.red;
					conditionalVDDOff();
					updateGUI(KONST.UpdateMemoryDisplays);
					return true;
				}
			}

			displayStatusWindow.Text = "Verifying Device:\n";
			//displayStatusWindow.Update();
			this.Update();

			if (!Pk2.FamilyIsKeeloq() && (!writeVerify || !Pk2.FamilyIsPIC24FJ()))
			{
				Pk2.SetMCLRTemp(true);     // assert /MCLR to prevent code execution before programming mode entered.
			}
			Pk2.VddOn();

			byte[] upload_buffer = new byte[KONST.UploadBufferSize];

			// compute configration information.
			int configLocation = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigAddr /
				Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemHexBytes;
			int configWords = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords;
			int endOfBuffer = Pk2.DeviceBuffers.ProgramMemory.Length - 1;
			if (writeVerify)
			{ // unless it's a write-verify
				endOfBuffer = lastLocation;
			}

			//Verify Program Memory ----------------------------------------------------------------------------
			if (checkBoxProgMemEnabled.Checked)
			{
				displayStatusWindow.Text += "Program Memory... ";
				//displayStatusWindow.Update();
				this.Update();

				if (useProgExec33())
				{
					if (!PE33.PE33VerifyCRC(displayStatusWindow.Text))
					{
						if (!writeVerify)
						{
							displayStatusWindow.Text = "Verification of Program Memory failed.\n";
						}
						else
						{
							displayStatusWindow.Text = "Programming of Program Memory failed.\n";
						}

						conditionalVDDOff();
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
				else if (useProgExec24F())
				{
					if (!PE24.PE24FVerify(displayStatusWindow.Text, writeVerify, lastLocation))
					{
						conditionalVDDOff();
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
				else
				{

					Pk2.RunScript(KONST.PROG_ENTRY, 1);

					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrSetScript != 0)
							&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrBytes != 0))
					{ // if prog mem address set script exists for this part
						if (Pk2.FamilyIsEEPROM())
						{
							Pk2.DownloadAddress3MSBFirst(eeprom24BitAddress(0, KONST.WRITE_BIT));
							Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
							if (!writeVerify)
							{
								if (eeprom_CheckBusErrors())
									return false;
							}
						}
						else
						{
							Pk2.DownloadAddress3(0);
							Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
						}
					}

					int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation;
					int scriptRunsToFillUpload = KONST.UploadBufferSize /
						(Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords * bytesPerWord);
					int wordsPerLoop = scriptRunsToFillUpload * Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords;
					int wordsRead = 0;
					if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords == (endOfBuffer + 1))
					{ // very small memory sizes (like HCS parts)
						scriptRunsToFillUpload = 1;
						wordsPerLoop = endOfBuffer + 1;
					}

					progressBar1.Value = 0;     // reset bar
					progressBar1.Maximum = (int)endOfBuffer / wordsPerLoop;

					do
					{
						if (Pk2.FamilyIsEEPROM())
						{
							if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
								&& (wordsRead > Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG])
								&& (wordsRead % (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] + 1) == 0))
							{
								Pk2.DownloadAddress3MSBFirst(eeprom24BitAddress(wordsRead, KONST.WRITE_BIT));
								Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
							}
							Pk2.Download3Multiples(eeprom24BitAddress(wordsRead, KONST.READ_BIT), scriptRunsToFillUpload,
										Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemRdWords);
						}
						//Pk2.RunScriptUploadNoLen2(KONST.PROGMEM_RD, scriptRunsToFillUpload);
						Pk2.RunScriptUploadNoLen(KONST.PROGMEM_RD, scriptRunsToFillUpload);
						Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
						//Pk2.GetUpload();
						Pk2.UploadDataNoLen();
						Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
						int uploadIndex = 0;
						for (int word = 0; word < wordsPerLoop; word++)
						{
							int bite = 0;
							uint memWord = (uint)upload_buffer[uploadIndex + bite++];
							if (bite < bytesPerWord)
							{
								memWord |= (uint)upload_buffer[uploadIndex + bite++] << 8;
							}
							if (bite < bytesPerWord)
							{
								memWord |= (uint)upload_buffer[uploadIndex + bite++] << 16;
							}
							if (bite < bytesPerWord)
							{
								memWord |= (uint)upload_buffer[uploadIndex + bite++] << 24;
							}
							uploadIndex += bite;
							// shift if necessary
							if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
							{
								memWord = (memWord >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
							}

							// swap "Endian-ness" of 16 bit 93LC EEPROMs
							if ((bytesPerWord == 2) && (Pk2.FamilyIsEEPROM())
								&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.MICROWIRE_BUS))
							{
								uint memTemp = 0;
								memTemp = (memWord << 8) & 0xFF00;
								memWord >>= 8;
								memWord |= memTemp;
							}

							if ((memWord != Pk2.DeviceBuffers.ProgramMemory[wordsRead++]) && !Pk2.LearnMode)
							{
								Pk2.RunScript(KONST.PROG_EXIT, 1);
								conditionalVDDOff();
								if (!writeVerify)
								{
									if (Pk2.FamilyIsEEPROM())
									{
										displayStatusWindow.Text = "Verification of EEPROM failed at address\n";
									}
									else
									{
										displayStatusWindow.Text = "Verification of Program Memory failed at address\n";
									}
								}
								else
								{
									if (Pk2.FamilyIsEEPROM())
									{
										displayStatusWindow.Text = "Programming failed at EEPROM address\n";
									}
									else
									{
										displayStatusWindow.Text = "Programming failed at Program Memory address\n";
									}
								}
								displayStatusWindow.Text += string.Format("0x{0:X6}",
									(--wordsRead * Pk2.DevFile.Families[Pk2.GetActiveFamily()].AddressIncrement));
								statusWindowColor = Constants.StatusColor.red;
								updateGUI(KONST.UpdateMemoryDisplays);
								return false;
							}

							if (((wordsRead % 0x8000) == 0)
									&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrSetScript != 0)
									&& (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemAddrBytes != 0)
									&& (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFF))
							{ //PIC24 must update TBLPAG
								Pk2.DownloadAddress3(0x10000 * (wordsRead / 0x8000));
								Pk2.RunScript(KONST.PROGMEM_ADDRSET, 1);
								break;
							}

							if (wordsRead > endOfBuffer)
							{
								break; // for cases where ProgramMemSize%WordsPerLoop != 0
							}
						}
						progressBar1.PerformStep();
					} while (wordsRead < endOfBuffer);
					Pk2.RunScript(KONST.PROG_EXIT, 1);
				}
			}

			//Verify EEPROM ------------------------------------------------------------------------------------
			if (((checkBoxEEMem.Checked) || forceEEVerify) && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
			{
				if (Pk2.LearnMode && (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0))
				{
					Pk2.MetaCmd_CHANGE_CHKSM_FRMT(2);
				}

				displayStatusWindow.Text += "EE... ";
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				if (Pk2.DevFile.PartsList[Pk2.ActivePart].EERdPrepScript > 0)
				{
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemHexBytes == 4)
					{ // 16-bit parts
						Pk2.DownloadAddress3((int)(Pk2.DevFile.PartsList[Pk2.ActivePart].EEAddr / 2));
					}
					else
					{
						Pk2.DownloadAddress3(0);
					}
					Pk2.RunScript(KONST.EE_RD_PREP, 1);
				}

				int bytesPerLoc = Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemBytesPerWord;
				int scriptRuns2FillUpload = KONST.UploadBufferSize /
					(Pk2.DevFile.PartsList[Pk2.ActivePart].EERdLocations * bytesPerLoc);
				int locPerLoop = scriptRuns2FillUpload * Pk2.DevFile.PartsList[Pk2.ActivePart].EERdLocations;
				int locsRead = 0;

				uint eeBlank = getEEBlank();

				progressBar1.Value = 0;     // reset bar
				progressBar1.Maximum = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem / locPerLoop;
				do
				{
					//Pk2.RunScriptUploadNoLen2(KONST.EE_RD, scriptRuns2FillUpload);
					Pk2.RunScriptUploadNoLen(KONST.EE_RD, scriptRuns2FillUpload);
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
					//Pk2.GetUpload();
					Pk2.UploadDataNoLen();
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
					int uploadIndex = 0;
					for (int word = 0; word < locPerLoop; word++)
					{
						int bite = 0;
						uint memWord = (uint)upload_buffer[uploadIndex + bite++];
						if (bite < bytesPerLoc)
						{
							memWord |= (uint)upload_buffer[uploadIndex + bite++] << 8;
						}
						uploadIndex += bite;
						// shift if necessary
						if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
						{
							memWord = (memWord >> 1) & eeBlank;
						}
						if ((memWord != Pk2.DeviceBuffers.EEPromMemory[locsRead++]) && !Pk2.LearnMode)
						{
							Pk2.RunScript(KONST.PROG_EXIT, 1);
							conditionalVDDOff();
							if (!writeVerify)
							{
								displayStatusWindow.Text = "Verification of EE Data Memory failed at address\n";
							}
							else
							{
								displayStatusWindow.Text = "Programming failed at EE Data address\n";
							}
							if (eeBlank == 0xFFFF)
							{
								displayStatusWindow.Text += string.Format("0x{0:X4}", (--locsRead * 2));
							}
							else
							{
								displayStatusWindow.Text += string.Format("0x{0:X4}", --locsRead);
							}
							statusWindowColor = Constants.StatusColor.red;
							updateGUI(KONST.UpdateMemoryDisplays);
							return false;
						}
						if (locsRead >= Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem)
						{
							break; // for cases where ProgramMemSize%WordsPerLoop != 0
						}
					}
					progressBar1.PerformStep();
				} while (locsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem);
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				if (Pk2.LearnMode && (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0))
				{
					Pk2.MetaCmd_CHANGE_CHKSM_FRMT(1);
				}
			}


			//Verify User IDs ----------------------------------------------------------------------------------
			if ((Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords > 0) && checkBoxProgMemEnabled.Checked)
			{ // When EE deselected, UserIDs are not programmed so don't try to verify.
				displayStatusWindow.Text += "UserIDs... ";
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDRdPrepScript > 0)
				{
					Pk2.RunScript(KONST.USERID_RD_PREP, 1);
				}
				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].UserIDBytes;
				int wordsRead = 0;
				int bufferIndex = 0;
				Pk2.RunScriptUploadNoLen(KONST.USERID_RD, 1);
				Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, 0, KONST.USB_REPORTLENGTH);
				if ((Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords * bytesPerWord) > KONST.USB_REPORTLENGTH)
				{
					Pk2.UploadDataNoLen();
					Array.Copy(Pk2.Usb_read_array, 1, upload_buffer, KONST.USB_REPORTLENGTH, KONST.USB_REPORTLENGTH);
				}
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				do
				{
					int bite = 0;
					uint memWord = (uint)upload_buffer[bufferIndex + bite++];
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 8;
					}
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 16;
					}
					if (bite < bytesPerWord)
					{
						memWord |= (uint)upload_buffer[bufferIndex + bite++] << 24;
					}
					bufferIndex += bite;
					// shift if necessary
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						memWord = (memWord >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
					}
					if ((memWord != Pk2.DeviceBuffers.UserIDs[wordsRead++]) && !Pk2.LearnMode)
					{
						conditionalVDDOff();
						if (!writeVerify)
						{
							displayStatusWindow.Text = "Verification of User IDs failed.";
						}
						else
						{
							displayStatusWindow.Text = "Programming failed at User IDs.";
						}
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				} while (wordsRead < Pk2.DevFile.PartsList[Pk2.ActivePart].UserIDWords);
			}

			if (!writeVerify)
			{ // don't check config if write-verify: it isn't written yet as it may contain code protection
				if (!verifyConfig(configWords, configLocation))
				{
					return false;
				}
			}


			Pk2.RunScript(KONST.PROG_EXIT, 1);

			if (!writeVerify)
			{
				statusWindowColor = Constants.StatusColor.green;
				displayStatusWindow.Text = "Verification Successful.\n";
				conditionalVDDOff();
			}

			updateGUI(KONST.UpdateMemoryDisplays);

			return true;
		}

		private bool verifyConfig(int configWords, int configLocation)
		{
			// Verify Configuration --------------------------------------------------------------------
			if ((configWords > 0) && (configLocation > Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem)
					&& checkBoxProgMemEnabled.Checked)
			{ // Don't read config words for any part where they are stored in program memory.
				displayStatusWindow.Text += "Config... ";
				//displayStatusWindow.Update();
				this.Update();
				Pk2.RunScript(KONST.PROG_ENTRY, 1);
				Pk2.RunScript(KONST.CONFIG_RD, 1);
				Pk2.UploadData();
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				int bufferIndex = 2;                    // report starts on index 1, which is #bytes uploaded.
				for (int word = 0; word < configWords; word++)
				{
					uint config = (uint)Pk2.Usb_read_array[bufferIndex++];
					config |= (uint)Pk2.Usb_read_array[bufferIndex++] << 8;
					if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemShift > 0)
					{
						config = (config >> 1) & Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue;
					}
					config &= Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[word];
					uint configExpected =
						Pk2.DeviceBuffers.ConfigWords[word] & Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[word];

					if (Pk2.DevFile.PartsList[Pk2.ActivePart].PartName == "PIC12F529")
					{
						// The PIC12F529 has "write only" bits in its config word. Mask those
						// bits from the expected value.
						configExpected |= 0x780;
					}

					if (word == Pk2.DevFile.PartsList[Pk2.ActivePart].CPConfig - 1)
					{
						if (enableCodeProtectToolStripMenuItem.Enabled && enableCodeProtectToolStripMenuItem.Checked)
						{
							configExpected &= (uint)~Pk2.DevFile.PartsList[Pk2.ActivePart].CPMask;
						}
						if (enableDataProtectStripMenuItem.Enabled && enableDataProtectStripMenuItem.Checked)
						{
							configExpected &= (uint)~Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask;
						}
					}
					if ((configExpected != config) && !Pk2.LearnMode)
					{
						conditionalVDDOff();
						displayStatusWindow.Text = "Verification of configuration failed.";
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
			}
			return true;
		}

		private void downloadPk2Firmware(object sender, EventArgs e)
		{
			if (openFWFile.ShowDialog() == DialogResult.OK)
			{
				if (!Pk2.isPK3)
					downloadNewFirmware();
				else
					downloadNewFirmwarePK3();
			}
		}

		private void downloadNewFirmware()
		{
			// download new firmware
			progressBar1.Value = 0;     // reset bar
			progressBar1.Maximum = 2;
			displayStatusWindow.Text = "Downloading New PICkit 2 Operating System.";
			displayStatusWindow.BackColor = Color.SteelBlue;
			this.Update();
			if (!Pk2BootLoader.ReadHexAndDownload(openFWFile.FileName, ref pk2number))
			{
				displayStatusWindow.Text = "Downloading failed.\nUse Tools > Check Communications to reconnect.";
				displayStatusWindow.BackColor = Color.Salmon;
				// shut down 
				downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;
				chkBoxVddOn.Enabled = false;
				numUpDnVDD.Enabled = false;
				deviceToolStripMenuItem.Enabled = false;
				disableGUIControls();
				return;
			}
			// verify new firmware
			progressBar1.PerformStep();
			displayStatusWindow.Text = "Verifying PICkit 2 Operating System.";
			this.Update();
			if (!Pk2BootLoader.ReadHexAndVerify(openFWFile.FileName))
			{
				displayStatusWindow.Text = "Operating System verification failed.";
				displayStatusWindow.BackColor = Color.Salmon;
				return;
			}

			// Write key 0x5555 at last memory location to tell bootloader FW loaded.
			if (!Pk2.BL_WriteFWLoadedKey())
			{
				displayStatusWindow.Text = "Error loading Operating System.";
				displayStatusWindow.BackColor = Color.Salmon;
				return;
			}

			// Reset PICkit 2
			progressBar1.PerformStep();
			displayStatusWindow.Text = "Verification Successful!\nWaiting for PICkit 2 to reset....";
			displayStatusWindow.BackColor = Color.LimeGreen;
			this.Update();
			Pk2.BL_Reset();

			Thread.Sleep(5000);

			Pk2.ResetPk2Number();

			if (!detectPICkit2(KONST.ShowMessage, true))
			{
				return;
			}

			Pk2.VddOff();

			lookForPoweredTarget(KONST.NoMessage);

			if (Pk2.DetectDevice(KONST.SEARCH_ALL_FAMILIES, true, chkBoxVddOn.Checked))
			{
				setGUIVoltageLimits(true);
				displayStatusWindow.Text = displayStatusWindow.Text + "\nPIC Device Found.";
				fullEnableGUIControls();
			}
			else
			{
				partialEnableGUIControls();
			}

			checkForPowerErrors();

			updateGUI(KONST.UpdateMemoryDisplays);

		}

		private void downloadNewFirmwarePK3()
		{
			KONST.PICkit2USB res;

			res = Pk2.DetectPICkit2Device(pk2number, true);
			if ((res == KONST.PICkit2USB.readwriteError) || (res == KONST.PICkit2USB.notFound) || (res == KONST.PICkit2USB.firmwareInvalid))
			{
				showDownloadError("PICkit 3 not found or communication error.  \nCheck USB connections and retry.");
				return;
			}

			// TODO Check what kind of firmware the PK3 has first. If it's a scripting OS then you should check for the Bootloader
			// if it's there then use it. If not then download a new AP bootloader
			// if it's an MPLAB RS, download bootloader. (Done)
			// if it's an MPLAB bootloader, send RS immediately. (Done)
			// if it's none of the above issue an error

			// TODO: do proper error propagation and checking all the way to the top

			// disable write on button
			timerButton.Enabled = false;
			writeOnPICkitButtonToolStripMenuItem.Checked = false;

			// download new firmware
			progressBar1.Value = 0;     // reset bar

			downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;

			if (res == KONST.PICkit2USB.pk3mplab) // TODO: check also we have the appropriate version
			{
				displayStatusWindow.Text = "Downloading PICkit 3 bootloader.";
				displayStatusWindow.BackColor = Color.SteelBlue;
				this.Update();

				res = Pk3h.WriteProgrammerOs(KONST.BLFileNamePk3, 0x23);

				//TODO: better way to choose bootloader
				if (res != KONST.PICkit2USB.bootloader)
				{
					showDownloadError("Downloading bootloader failed. Check USB \nconnections and use Tools->Check Communication to retry.");
					return;
				}
			}
			else if (res == KONST.PICkit2USB.found || res == KONST.PICkit2USB.firmwareOldversion)
			{
				// We need to switch to Boot loader so we can start downloading a new OS
				displayStatusWindow.Text = "Switching to PICkit 3 bootloader.";
				displayStatusWindow.BackColor = Color.SteelBlue;
				this.Update();

				if (!Pk2.TestPk3BootloaderExists())
				{
					displayStatusWindow.Text = "No bootloader found on PICkit 3.\nDownloading firmware aborted.";
					displayStatusWindow.BackColor = Color.Salmon;
					return;
				}

				KONST.MPLABerrorcodes errorcode = KONST.MPLABerrorcodes.Unknownerror;
				res = KONST.PICkit2USB.notFound;
				bool s = Pk2.SwitchToBootLoader(ref errorcode);
				if (s)
				{
					Thread.Sleep(2000); // Delay a bit so the PICkit 3 restarts
					while (res == KONST.PICkit2USB.notFound)
					{
						res = Pk2.DetectPICkit2Device(pk2number, true);
						Thread.Sleep(500); // Delay a bit so we don't hammer the programmer
					}

				}

				if (res != KONST.PICkit2USB.bootloader)
				{
					showDownloadError("Downloading bootloader failed. Check USB \nconnections and use Tools->Check Communication.");
					return;
				}
				// verify new firmware
				//progressBar1.PerformStep();
			}

			displayStatusWindow.Text = "Downloading PICkit 3 Operating System.";
			this.Update();

			//TODO: have a command line option to have a default file name
			res = Pk3h.WriteProgrammerOs(openFWFile.FileName, 0x22);

			if (res == KONST.PICkit2USB.found)
			{
				//progressBar1.PerformStep();
				displayStatusWindow.Text = "Download Successful!\nReconnecting....";
				displayStatusWindow.BackColor = Color.LimeGreen;
				this.Update();
			}

			if (!detectPICkit2(KONST.ShowMessage, true))
			{
				showDownloadError("Downloading Operating System failed. Check USB \nconnections and use Tools->Check Communication.");
				return;
			}

			progressBar1.Value = 0;     // reset bar

			Pk2.VddOff();

			lookForPoweredTarget(KONST.NoMessage);

			if (Pk2.DetectDevice(KONST.SEARCH_ALL_FAMILIES, true, chkBoxVddOn.Checked))
			{
				setGUIVoltageLimits(true);
				displayStatusWindow.Text = displayStatusWindow.Text + "\nPIC Device Found.";
				fullEnableGUIControls();
			}
			else
			{
				partialEnableGUIControls();
			}

			checkForPowerErrors();

			updateGUI(KONST.UpdateMemoryDisplays);

		}

		private void programmingSpeed(object sender, EventArgs e)
		{
			if (fastProgrammingToolStripMenuItem.Checked)
			{
				Pk2.SetFastProgramming(true);
				displayStatusWindow.BackColor = System.Drawing.SystemColors.Info;
				if (Pk2.FamilyIsEEPROM())
				{
					if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
					{
						displayStatusWindow.Text = "Fast programming On- 400kHz I2C\n";
					}
					else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
					{
						displayStatusWindow.Text = "Fast programming On- FBUS = 100kHz\n";
					}
					else
					{
						displayStatusWindow.Text = "Fast programming On- 925kHz SCK\n";
					}
				}
				else
				{
					displayStatusWindow.Text = "Fast programming On- Programming operations\n";
					displayStatusWindow.Text +=
						"are faster, but less tolerant of loaded ICSP lines.";
				}
			}
			else
			{
				Pk2.SetFastProgramming(false);
				displayStatusWindow.BackColor = System.Drawing.SystemColors.Info;
				if (Pk2.FamilyIsEEPROM())
				{
					if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
					{
						displayStatusWindow.Text = "Fast programming Off - 100kHz I2C\n";
					}
					else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
					{
						displayStatusWindow.Text = "Fast programming Off- FBUS = 25kHz\n";
					}
					else
					{
						displayStatusWindow.Text = "Fast programming Off - 245kHz SCK\n";
					}
				}
				else
				{
					displayStatusWindow.Text = "Fast programming Off - Programming operations\n";
					displayStatusWindow.Text +=
						"are slower, but more tolerant of loaded ICSP lines.";
				}
			}
		}

		private void clickAbout(object sender, EventArgs e)
		{
			DialogAbout aboutWindow = new DialogAbout();
			aboutWindow.ShowDialog();
		}

		private void launchUsersGuide(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + KONST.UserGuideFileNamePK2);
			}
			catch
			{
				MessageBox.Show("Unable to open User's Guide.");
			}
		}

		private void launchReadMe(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + "\\PICkit 2 Readme.txt");
			}
			catch
			{
				MessageBox.Show("Unable to open ReadMe file.");
			}
		}

		private void betaReleaseReadMeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + "\\PICkit 3 Programmer Application ReadMe.txt");
			}
			catch
			{
				MessageBox.Show("Unable to open ReadMe file.");
			}
		}

		private void codeProtect(object sender, EventArgs e)
		{
			if (enableDataProtectStripMenuItem.Enabled && (Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask == 0))
			{ // if part has EE but no CPMask, then both CP/DP get selected at same time (ie dsPIC30)
				enableDataProtectStripMenuItem.Checked = enableCodeProtectToolStripMenuItem.Checked;
			}
			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private void dataProtect(object sender, EventArgs e)
		{
			if (enableDataProtectStripMenuItem.Enabled && (Pk2.DevFile.PartsList[Pk2.ActivePart].DPMask == 0))
			{ // if part has EE but no CPMask, then both CP/DP get selected at same time (ie dsPIC30)
				enableCodeProtectToolStripMenuItem.Checked = enableDataProtectStripMenuItem.Checked;
			}
			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private void writeOnButton(object sender, EventArgs e)
		{
			if (writeOnPICkitButtonToolStripMenuItem.Checked)
			{
				timerButton.Enabled = true;
				buttonLast = true;
				displayStatusWindow.Text = "Waiting for " + Pk2.ToolName + " button to be pressed....";
			}
			else
			{
				timerButton.Enabled = false;
			}

		}

		private void timerGoesOff(object sender, EventArgs e)
		{
			/*if (!detectPICkit2(KONST.NoMessage))
			{
				return;
			}*/

			if (!Pk2.ButtonPressed())
			{
				buttonLast = false;
				return;
			}
			if (buttonLast)
			{
				return;
			}

			buttonLast = true;

			deviceWrite();

			checkForPowerErrors();
		}

		private void conditionalVDDOff()
		{
			if (!chkBoxVddOn.Checked)
			{ // don't shut off if Vdd is "ON"
				Pk2.VddOff();
			}
		}

		private void buttonReadExport(object sender, EventArgs e)
		{
			if (Pk2.FamilyIsKeeloq())
			{
				displayStatusWindow.Text = "Read not supported for this device type.";
				statusWindowColor = Constants.StatusColor.yellow;
				updateGUI(KONST.DontUpdateMemDisplays);
				return; // abort
			}

			// read device
			deviceRead();

			updateGUI(KONST.UpdateMemoryDisplays);
			this.Refresh();

			// export hex
			saveHexFileDialog.ShowDialog();
		}

		private void menuVDDAuto(object sender, EventArgs e)
		{
			vddAuto();
		}
		private void vddAuto()
		{
			autoDetectToolStripMenuItem.Checked = true;
			forcePICkit2ToolStripMenuItem.Checked = false;
			forceTargetToolStripMenuItem.Checked = false;
			lookForPoweredTarget(false);
		}

		private void menuVDDPk2(object sender, EventArgs e)
		{
			vddPk2();
		}
		private void vddPk2()
		{
			autoDetectToolStripMenuItem.Checked = false;
			forcePICkit2ToolStripMenuItem.Checked = true;
			forceTargetToolStripMenuItem.Checked = false;
			lookForPoweredTarget(false);
		}

		private void menuVDDTarget(object sender, EventArgs e)
		{
			vddTarget();
		}
		private void vddTarget()
		{
			autoDetectToolStripMenuItem.Checked = false;
			forcePICkit2ToolStripMenuItem.Checked = false;
			forceTargetToolStripMenuItem.Checked = true;
			lookForPoweredTarget(false);
		}

		private void deviceFamilyClick(object sender, ToolStripItemClickedEventArgs e)
		{
			ToolStripMenuItem itemSelected = (ToolStripMenuItem)e.ClickedItem;
			if (itemSelected.HasDropDownItems)
			{// it's a submenu
				return; // do nothing.
			}

			string familyName = "";
			labelConfig.Enabled = false; // until a device is selected

			if (itemSelected.OwnerItem.Equals(deviceToolStripMenuItem))
			{ // it's a top level family
				familyName = itemSelected.Text;
			}
			else
			{ // submenu family
				familyName = itemSelected.OwnerItem.Text + "/" + itemSelected.Text;
			}

			// find family ID
			int family = 0;
			for (; family < Pk2.DevFile.Families.Length; family++)
			{
				if (familyName == Pk2.DevFile.Families[family].FamilyName)
					break;
			}

			// Set unsupported part to voltages of first part in the selected family
			for (int p = 1; p < Pk2.DevFile.Info.NumberParts; p++)
			{ // start at 1 so don't search Unsupported Part
				if (Pk2.DevFile.PartsList[p].Family == family)
				{
					Pk2.DevFile.PartsList[0].VddMax = Pk2.DevFile.PartsList[p].VddMax;
					Pk2.DevFile.PartsList[0].VddMin = Pk2.DevFile.PartsList[p].VddMin;
					break;
				}
			}

			FamilySelectLogic(family, true);

		}

		private void FamilySelectLogic(int family, bool updateGUI_OK)
		{
			if (family != Pk2.GetActiveFamily())
			{
				Pk2.ActivePart = 0;         // no part is last part in new family
				setGUIVoltageLimits(true);  // reset voltage on new family
			}
			else
			{
				setGUIVoltageLimits(false); // keep last voltage set if same family
			}

			displayStatusWindow.Text = "";

			if (Pk2.DevFile.Families[family].PartDetect)
			{ // searchable families
				Pk2.SetActiveFamily(family);
				//setGUIVoltageLimits(false);
				if (preProgrammingCheck(family))
				{
					displayStatusWindow.Text = Pk2.DevFile.Families[family].FamilyName + " device found.";
					setGUIVoltageLimits(false);
				}
				comboBoxSelectPart.Visible = false;
				displayDevice.Visible = true;
				if (updateGUI_OK)
					updateGUI(KONST.UpdateMemoryDisplays);
			}
			else
			{ // drop-down select families
				buildDeviceSelectDropDown(family);
				comboBoxSelectPart.Visible = true;
				comboBoxSelectPart.SelectedIndex = 0;
				displayDevice.Visible = true;
				Pk2.SetActiveFamily(family);
				//setGUIVoltageLimits(true);
				if (updateGUI_OK)
					updateGUI(KONST.UpdateMemoryDisplays);
				disableGUIControls();
			}

			displayDataSource.Text = "None (Empty/Erased)";
		}

		private void buildDeviceSelectDropDown(int family)
		{
			comboBoxSelectPart.Items.Clear();
			comboBoxSelectPart.Items.Add("-Select Part-");
			for (int part = 1; part < Pk2.DevFile.Info.NumberParts; part++)
			{
				if (Pk2.DevFile.PartsList[part].Family == family)
				{
					comboBoxSelectPart.Items.Add(Pk2.DevFile.PartsList[part].PartName);
				}

			}
		}

		private void selectPart(object sender, EventArgs e)
		{
			if (comboBoxSelectPart.SelectedIndex == 0)
			{
				disableGUIControls();
			}
			else
			{
				string partName = comboBoxSelectPart.SelectedItem.ToString();
				fullEnableGUIControls();
				for (int part = 0; part < Pk2.DevFile.Info.NumberParts; part++)
				{
					if (Pk2.DevFile.PartsList[part].PartName == partName)
					{
						Pk2.ActivePart = part;
						break;
					}
				}
			}
			// Set up for a new device
			Pk2.PrepNewPart(true);
			setGUIVoltageLimits(true);
			displayDataSource.Text = "None (Empty/Erased)";
			if (useLVP)
			{ // Enable LVP on first device selected if supported
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].LVPScript > 0)
					toolStripMenuItemLVPEnabled.Checked = true;
			}
			useLVP = false;
			updateGUI(KONST.UpdateMemoryDisplays);
		}

		private void autoDownloadFW(object sender, EventArgs e)
		{
			timerDLFW.Enabled = false;
			displayStatusWindow.Text =
				"The " + Pk2.ToolName + " Operating System v" + Pk2.FirmwareVersion + " must be updated.";

			DialogResult download = MessageBox.Show(
				 Pk2.ToolName + " Operating System must be updated\nbefore it can be used with this software\nversion.\n\nClick OK to download a new Operating System.",
				"Update Operating System", MessageBoxButtons.OKCancel);

			if (download == DialogResult.OK)
			{
				if (!Pk2.isPK3)
				{
					openFWFile.FileName = KONST.FWFileName;
					downloadNewFirmware();
				}
				else
				{
					openFWFile.FileName = KONST.FWFileNamePk3;
					downloadNewFirmwarePK3();
				}
				oldFirmware = false;
			}
			else
			{
				displayStatusWindow.Text = "The " + Pk2.ToolName + " OS v" + Pk2.FirmwareVersion +
				" must be updated.\nUse the Tools menu to download a new OS.";
			}

		}

		private void SaveINI()
		{
			//StreamWriter hexFile = new StreamWriter("PICkit2.ini");
			StreamWriter hexFile;

			if (!Pk2.isPK3)
				hexFile = File.CreateText(HomeDirectory + "\\PICkit2.ini");
			else
				hexFile = File.CreateText(HomeDirectory + "\\PICkit3.ini");

			// Comments
			string value = ";" + Pk2.ToolName + " version " + Constants.AppVersion + " INI file.";
			hexFile.WriteLine(value);
			DateTime now = new DateTime();
			now = System.DateTime.Now;
			value = ";" + now.Date.ToShortDateString() + " " + now.ToShortTimeString();
			hexFile.WriteLine(value);
			hexFile.WriteLine("");

			// auto-detect parts for applicable families
			value = "Y";
			if (toolStripMenuItemManualSelect.Checked)
			{
				value = "N";
				searchOnStartup = false;
			}
			else if (!autoDetectInINI)
			{ // if menaul select was just changed back to auto
				searchOnStartup = true; // also enable this.
			}
			hexFile.WriteLine("ADET: " + value);

			// auto-detect parts on appliction startup
			value = "N";
			if (searchOnStartup)
			{
				value = "Y";
			}
			hexFile.WriteLine("PDET: " + value);

			// write last family
			if (Pk2.DevFile.Families == null)
				value = lastFamily;
			else
				value = Pk2.DevFile.Families[Pk2.GetActiveFamily()].FamilyName;
			hexFile.WriteLine("LFAM: " + value);

			// verify on write state
			value = "N";
			if (verifyOnWriteToolStripMenuItem.Checked)
			{
				value = "Y";
			}
			hexFile.WriteLine("VRFW: " + value);

			// program on pickit button state
			value = "N";
			if (writeOnPICkitButtonToolStripMenuItem.Checked)
			{
				value = "Y";
			}
			hexFile.WriteLine("WRBT: " + value);

			// hold device in reset state
			value = "N";
			if (MCLRtoolStripMenuItem.Checked)
			{
				value = "Y";
			}
			hexFile.WriteLine("MCLR: " + value);

			// target VDD source option
			if (VppFirstToolStripMenuItem.Checked)
			{ // app closed with VPP first checked.
				restoreVddTarget();
			}
			value = "Auto";
			if (forcePICkit2ToolStripMenuItem.Checked)
			{
				value = "PICkit";
			}
			else if (forceTargetToolStripMenuItem.Checked)
			{
				value = "Target";
			}
			hexFile.WriteLine("TVDD: " + value);

			// fast programming
			value = "N";
			if (fastProgrammingToolStripMenuItem.Checked)
			{
				value = "Y";
			}
			hexFile.WriteLine("FPRG: " + value);

			// "slow" programming speed
			value = string.Format("PCLK: {0:G}", slowSpeedICSP);
			hexFile.WriteLine(value);

			// program memory view ASCII
			value = "N";
			if (multiWindow)
				comboBoxProgMemView.SelectedIndex = programMemMultiWin.GetViewMode();
			if (comboBoxProgMemView.SelectedIndex == 1)
			{
				value = "Y"; // word view
			}
			else if (comboBoxProgMemView.SelectedIndex == 2)
			{
				value = "B"; // byte view
			}
			hexFile.WriteLine("PASC: " + value);

			// EEPROM view ASCII
			value = "N";
			if (multiWindow)
				comboBoxProgMemView.SelectedIndex = eepromDataMultiWin.GetViewMode();
			if (comboBoxEE.SelectedIndex == 1)
			{
				value = "Y";
			}
			else if (comboBoxEE.SelectedIndex == 2)
			{
				value = "B";
			}
			hexFile.WriteLine("EASC: " + value);

			// Memory Editing
			value = "N";
			if (allowDataEdits)
			{
				value = "Y";
			}
			hexFile.WriteLine("EDIT: " + value);

			// Display Revisions
			if (displayRev.Visible)
			{
				hexFile.WriteLine("REVS: Y");
			}

			// VDD set value
			value = string.Format("SETV: {0:0.0}", numUpDnVDD.Value);
			hexFile.WriteLine(value);

			// Clear buffers on erase
			value = "N";
			if (toolStripMenuItemClearBuffersErase.Checked)
			{
				value = "Y";
			}
			hexFile.WriteLine("CLBF: " + value);

			// Use PIC24H/dsPIC33 PE
			value = "N";
			if (usePE33)
			{
				value = "Y";
			}
			hexFile.WriteLine("PE33: " + value);

			// Use PIC24F PE
			value = "N";
			if (usePE24)
			{
				value = "Y";
			}
			hexFile.WriteLine("PE24: " + value);

			// Config word display options.
			value = "0";
			if (as1BitValueToolStripMenuItem.Checked)
			{
				value = "1";
			}
			else if (asReadOrImportedToolStripMenuItem.Checked)
			{
				value = "R";
			}
			hexFile.WriteLine("CFGU: " + value);

			// Use LVP
			value = "N";
			if (toolStripMenuItemLVPEnabled.Checked)
			{
				value = "Y";
			}
			hexFile.WriteLine("LVPE: " + value);

			// manual select device verify
			value = "N";
			if (deviceVerification)
			{
				value = "Y";
			}
			hexFile.WriteLine("DVER: " + value);

			// HEX file shortcuts
			hexFile.WriteLine("HEX1: " + hex1);
			hexFile.WriteLine("HEX2: " + hex2);
			hexFile.WriteLine("HEX3: " + hex3);
			hexFile.WriteLine("HEX4: " + hex4);

			// Select Device File
			if (selectDeviceFile)
			{ // doesn't save if false
				hexFile.WriteLine("SDAT: Y");
			}

			// Test Memory State
			if (TestMemoryEnabled)
			{
				value = "N";
				if (TestMemoryOpen)
				{
					value = "Y";
				}
				hexFile.WriteLine("TMEN: " + value);
				// TestMemoryWords
				value = string.Format("TMWD: {0:G}", TestMemoryWords);
				hexFile.WriteLine(value);
				// TestMemory Import/Exporting
				value = "N";
				if (TestMemoryImportExport)
				{
					value = "Y";
				}
				hexFile.WriteLine("TMIE: " + value);
			}

			// Window VIEW settings -----------------------------------------------------
			// Multi-Window
			value = "N";
			if (multiWindow)
			{
				value = "Y";
			}
			hexFile.WriteLine("MWEN: " + value);

			// Main window location
			value = string.Format("MWLX: {0:G}", this.Location.X);
			hexFile.WriteLine(value);
			value = string.Format("MWLY: {0:G}", this.Location.Y);
			hexFile.WriteLine(value);
			// Main window front
			value = "N";
			if (mainWinOwnsMem)
			{
				value = "Y";
			}
			hexFile.WriteLine("MWFR: " + value);
			// Program memory window open
			value = "N";
			if (multiWinPMemOpen)
			{
				value = "Y";
			}
			hexFile.WriteLine("PMEN: " + value);
			// Program Memory window location
			value = string.Format("PMLX: {0:G}", programMemMultiWin.Location.X);
			hexFile.WriteLine(value);
			value = string.Format("PMLY: {0:G}", programMemMultiWin.Location.Y);
			hexFile.WriteLine(value);
			// Program Memory window size
			value = string.Format("PMSX: {0:G}", programMemMultiWin.Size.Width);
			hexFile.WriteLine(value);
			value = string.Format("PMSY: {0:G}", programMemMultiWin.Size.Height);
			hexFile.WriteLine(value);
			// EEPROM memory window open
			value = "N";
			if (multiWinEEMemOpen)
			{
				value = "Y";
			}
			hexFile.WriteLine("EEEN: " + value);
			// EEPROM Data window location
			value = string.Format("EELX: {0:G}", eepromDataMultiWin.Location.X);
			hexFile.WriteLine(value);
			value = string.Format("EELY: {0:G}", eepromDataMultiWin.Location.Y);
			hexFile.WriteLine(value);
			// EEPROM Data window size
			value = string.Format("EESX: {0:G}", eepromDataMultiWin.Size.Width);
			hexFile.WriteLine(value);
			value = string.Format("EESY: {0:G}", eepromDataMultiWin.Size.Height);
			hexFile.WriteLine(value);


			// UART Tool settings -------------------------------------------------------
			// Baud Rate
			hexFile.WriteLine("UABD: " + uartWindow.GetBaudRate());

			// mode
			value = "N";
			if (uartWindow.IsHexMode())
			{
				value = "Y";
			}
			hexFile.WriteLine("UAHX: " + value);

			// String Macros
			hexFile.WriteLine("UAS1: " + uartWindow.GetStringMacro(1));
			hexFile.WriteLine("UAS2: " + uartWindow.GetStringMacro(2));
			hexFile.WriteLine("UAS3: " + uartWindow.GetStringMacro(3));
			hexFile.WriteLine("UAS4: " + uartWindow.GetStringMacro(4));

			// Append CR & LF
			value = "N";
			if (uartWindow.GetAppendCRLF())
			{
				value = "Y";
			}
			hexFile.WriteLine("UACL: " + value);

			// Wrap
			value = "N";
			if (uartWindow.GetWrap())
			{
				value = "Y";
			}
			hexFile.WriteLine("UAWR: " + value);

			// Echo
			value = "N";
			if (uartWindow.GetEcho())
			{
				value = "Y";
			}
			hexFile.WriteLine("UAEC: " + value);

			// Logic Tool settings -------------------------------------------------------
			value = "N";
			if (logicWindow.getModeAnalyzer())
			{
				value = "Y";
			}
			hexFile.WriteLine("LTAM: " + value);

			value = string.Format("LTZM: {0:G}", logicWindow.getZoom());
			hexFile.WriteLine(value);

			value = string.Format("LTT1: {0:G}", logicWindow.getCh1Trigger());
			hexFile.WriteLine(value);
			value = string.Format("LTT2: {0:G}", logicWindow.getCh2Trigger());
			hexFile.WriteLine(value);
			value = string.Format("LTT3: {0:G}", logicWindow.getCh3Trigger());
			hexFile.WriteLine(value);

			value = string.Format("LTTC: {0:G}", logicWindow.getTrigCount());
			hexFile.WriteLine(value);

			value = string.Format("LTSR: {0:G}", logicWindow.getSampleRate());
			hexFile.WriteLine(value);

			value = string.Format("LTTP: {0:G}", logicWindow.getTriggerPosition());
			hexFile.WriteLine(value);

			value = "N";
			if (logicWindow.getCursorsEnabled())
			{
				value = "Y";
			}
			hexFile.WriteLine("LTCE: " + value);

			value = string.Format("LTCX: {0:G}", logicWindow.getXCursorPos());
			hexFile.WriteLine(value);
			value = string.Format("LTCY: {0:G}", logicWindow.getYCursorPos());
			hexFile.WriteLine(value);

			// Programmer-To-Go settings -------------------------------------------------------
			// Memory size
			// mode
			value = "0";
			if ((ptgMemory > 0) && (ptgMemory <= 5))
			{
				if (ptgMemory == 1) value = "1";    // save the PTGM setting. 256K I2C EEPROM ===== 
				else if (ptgMemory == 2) value = "2";    // save the PTGM setting. 512K SPI Flash ===== 
				else if (ptgMemory == 3) value = "3";    // save the PTGM setting. 1M SPI Flash ===== 
				else if (ptgMemory == 4) value = "4";    // save the PTGM setting. 2M SPI Flash ===== 
				else if (ptgMemory == 5) value = "5";    // save the PTGM setting. 4M SPI Flash =====                 
			}
			hexFile.WriteLine("PTGM: " + value);

			// Alert Sound settings -------------------------------------------------------
			value = "N";
			if (PlaySuccessWav)
			{
				value = "Y";
			}
			hexFile.WriteLine("SDSP: " + value);

			value = "N";
			if (PlayWarningWav)
			{
				value = "Y";
			}
			hexFile.WriteLine("SDWP: " + value);

			value = "N";
			if (PlayErrorWav)
			{
				value = "Y";
			}
			hexFile.WriteLine("SDEP: " + value);

			hexFile.WriteLine("SDSF: " + SuccessWavFile);
			hexFile.WriteLine("SDWF: " + WarningWavFile);
			hexFile.WriteLine("SDEF: " + ErrorWavFile);

			hexFile.Flush();
			hexFile.Close();
		}

		private float loadINI()
		{
			float returnSETV = 0;
			int tempval = 0;

			try
			{
				// get desktop size:
				int desktopHeigth = SystemInformation.VirtualScreen.Height;
				int desktopWidth = SystemInformation.VirtualScreen.Width;

				FileInfo hexFile;

				if (!Pk2.isPK3)
					hexFile = new FileInfo("PICkit2.ini");
				else
					hexFile = new FileInfo("PICkit3.ini");

				HomeDirectory = hexFile.DirectoryName;
				// init default sounds locations
				SuccessWavFile = HomeDirectory + SuccessWavFile;
				WarningWavFile = HomeDirectory + WarningWavFile;
				ErrorWavFile = HomeDirectory + ErrorWavFile;
				TextReader hexRead = hexFile.OpenText();
				string fileLine = hexRead.ReadLine();
				while (fileLine != null)
				{
					if ((fileLine != "") && (string.Compare(fileLine.Substring(0, 1), ";") != 0)
							&& (string.Compare(fileLine.Substring(0, 1), " ") != 0))
					{
						string parameter = fileLine.Substring(0, 5);
						switch (parameter)
						{
							case "ADET:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									toolStripMenuItemManualSelect.Checked = true;
									autoDetectInINI = false;
									searchOnStartup = false;
								}
								break;

							case "PDET:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									searchOnStartup = false;
								}
								break;

							case "LFAM:":
								lastFamily = fileLine.Substring(6);
								break;

							case "VRFW:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									verifyOnWriteToolStripMenuItem.Checked = false;
								}
								break;

							case "WRBT:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									writeOnPICkitButtonToolStripMenuItem.Checked = true;
									timerButton.Enabled = true;
									buttonLast = true;
								}
								break;

							case "MCLR:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									MCLRtoolStripMenuItem.Checked = true;
									checkBoxMCLR.Checked = true;
									Pk2.HoldMCLR(true);
								}
								break;

							case "TVDD:":
								if (string.Compare(fileLine.Substring(6, 1), "P") == 0)
								{
									vddPk2();
								}
								else if (string.Compare(fileLine.Substring(6, 1), "T") == 0)
								{
									vddTarget();
								}
								break;

							case "FPRG:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									fastProgrammingToolStripMenuItem.Checked = false;
									Pk2.SetFastProgramming(false);
								}
								break;

							case "PCLK:":
								if (fileLine.Length == 7)
								{
									slowSpeedICSP = byte.Parse(fileLine.Substring(6, 1));
								}
								else
								{
									slowSpeedICSP = byte.Parse(fileLine.Substring(6, 2));
								}

								if (slowSpeedICSP < 2)
								{
									slowSpeedICSP = 2;
								}
								if (slowSpeedICSP > 16)
								{
									slowSpeedICSP = 16;
								}
								break;

							case "PASC:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									comboBoxProgMemView.SelectedIndex = 1;
								}
								else if (string.Compare(fileLine.Substring(6, 1), "B") == 0)
								{
									comboBoxProgMemView.SelectedIndex = 2;
								}
								break;

							case "EASC:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									comboBoxEE.SelectedIndex = 1;
								}
								else if (string.Compare(fileLine.Substring(6, 1), "B") == 0)
								{
									comboBoxEE.SelectedIndex = 2;
								}
								break;

							case "EDIT:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									allowDataEdits = false;
									calibrateToolStripMenuItem.Visible = false;
								}
								break;

							case "REVS:":
								displayRev.Visible = true;
								break;

							case "SETV:":
								if (fileLine.Length == 9)
								{
									returnSETV = float.Parse(fileLine.Substring(6, 3));
									if (returnSETV > 5.0F)
									{
										returnSETV = 5.0F;
									}
									if (returnSETV < 2.5)
									{
										returnSETV = 2.5F;
									}
								}
								else
								{
									returnSETV = 0F;    // invalid entry
								}
								break;

							case "CLBF:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									toolStripMenuItemClearBuffersErase.Checked = false;
								}
								break;

							case "PE33:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									usePE33 = false;
								}
								break;

							case "PE24:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									usePE24 = false;
								}
								break;

							case "CFGU:":
								if (string.Compare(fileLine.Substring(6, 1), "1") == 0)
								{
									as0BitValueToolStripMenuItem.Checked = false;
									as1BitValueToolStripMenuItem.Checked = true;
								}
								else if (string.Compare(fileLine.Substring(6, 1), "R") == 0)
								{
									as0BitValueToolStripMenuItem.Checked = false;
									asReadOrImportedToolStripMenuItem.Checked = true;
								}
								break;

							case "LVPE:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									useLVP = true;
								}
								break;

							case "DVER:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									deviceVerification = false;
								}
								break;

							case "HEX1:":
								hex1 = fileLine.Substring(6);
								if (hex1.Length > 3)
								{
									hex1ToolStripMenuItem.Visible = true;
									toolStripMenuItem5.Visible = true;
								}
								break;

							case "HEX2:":
								hex2 = fileLine.Substring(6);
								if (hex2.Length > 3)
								{
									hex2ToolStripMenuItem.Visible = true;
								}
								break;

							case "HEX3:":
								hex3 = fileLine.Substring(6);
								if (hex3.Length > 3)
								{
									hex3ToolStripMenuItem.Visible = true;
								}
								break;

							case "HEX4:":
								hex4 = fileLine.Substring(6);
								if (hex4.Length > 3)
								{
									hex4ToolStripMenuItem.Visible = true;
								}
								break;

							case "SDAT:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									selectDeviceFile = true;
								}
								break;

							case "TMEN:":
								TestMemoryEnabled = true;
								if (fileLine.Length > 5)
								{
									if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
									{
										TestMemoryOpen = true;
									}
								}
								break;

							case "TMWD:":
								TestMemoryWords = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (TestMemoryWords < 16)
								{
									TestMemoryWords = 16;
								}
								if (TestMemoryWords > 1024)
								{
									TestMemoryWords = 1024;
								}
								break;

							case "TMIE:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									TestMemoryImportExport = true;
								}
								break;

							// Window VIEW settings ------------------------------------------------
							case "MWEN:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									multiWindow = true;
									viewChanged = true;
								}
								break;

							case "MWLX:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 0)
									tempval = 0;
								if (tempval > desktopWidth)
									tempval = desktopWidth - 75;
								this.Location = new Point(tempval, this.Location.Y);
								break;

							case "MWLY:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 0)
									tempval = 0;
								if (tempval > desktopHeigth)
									tempval = desktopHeigth - 75;
								this.Location = new Point(this.Location.X, tempval);
								break;

							case "MWFR:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									mainWinOwnsMem = false;
								}
								break;

							case "PMEN:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									multiWinPMemOpen = false;
								}
								break;

							case "PMLX:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 0)
									tempval = 0;
								if (tempval > desktopWidth)
									tempval = desktopWidth - 75;
								programMemMultiWin.Location = new Point(tempval, programMemMultiWin.Location.Y);
								break;

							case "PMLY:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 0)
									tempval = 0;
								if (tempval > desktopHeigth)
									tempval = desktopHeigth - 75;
								programMemMultiWin.Location = new Point(programMemMultiWin.Location.X, tempval);
								break;

							case "PMSX:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 50)
									tempval = 50;
								if (tempval > desktopWidth)
									tempval = desktopWidth;
								programMemMultiWin.Size = new Size(tempval, programMemMultiWin.Size.Height);
								break;

							case "PMSY:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 50)
									tempval = 50;
								if (tempval > desktopHeigth)
									tempval = desktopHeigth;
								programMemMultiWin.Size = new Size(programMemMultiWin.Size.Width, tempval);
								break;

							case "EEEN:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									multiWinEEMemOpen = false;
								}
								break;

							case "EELX:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 0)
									tempval = 0;
								if (tempval > desktopWidth)
									tempval = desktopWidth - 75;
								eepromDataMultiWin.Location = new Point(tempval, eepromDataMultiWin.Location.Y);
								break;

							case "EELY:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 0)
									tempval = 0;
								if (tempval > desktopHeigth)
									tempval = desktopHeigth - 75;
								eepromDataMultiWin.Location = new Point(eepromDataMultiWin.Location.X, tempval);
								break;

							case "EESX:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 50)
									tempval = 50;
								if (tempval > desktopWidth)
									tempval = desktopWidth;
								eepromDataMultiWin.Size = new Size(tempval, eepromDataMultiWin.Size.Height);
								break;

							case "EESY:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval < 50)
									tempval = 50;
								if (tempval > desktopHeigth)
									tempval = desktopHeigth;
								eepromDataMultiWin.Size = new Size(eepromDataMultiWin.Size.Width, tempval);
								break;


							// UART Tool settings --------------------------------------------------
							case "UABD:":
								uartWindow.SetBaudRate(fileLine.Substring(6));
								break;

							case "UAHX:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									uartWindow.SetModeHex();
								}
								break;

							case "UAS1:":
								uartWindow.SetStringMacro(fileLine.Substring(6), 1);
								break;
							case "UAS2:":
								uartWindow.SetStringMacro(fileLine.Substring(6), 2);
								break;
							case "UAS3:":
								uartWindow.SetStringMacro(fileLine.Substring(6), 3);
								break;
							case "UAS4:":
								uartWindow.SetStringMacro(fileLine.Substring(6), 4);
								break;

							case "UACL:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									uartWindow.ClearAppendCRLF();
								}
								break;

							case "UAWR:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									uartWindow.ClearWrap();
								}
								break;

							case "UAEC:":
								if (string.Compare(fileLine.Substring(6, 1), "N") == 0)
								{
									uartWindow.ClearEcho();
								}
								break;

							// Logic Tool settings ------------------------------------------------
							case "LTAM:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									logicWindow.setModeAnalyzer();
								}
								break;

							case "LTZM:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 3)
									tempval = 3;
								logicWindow.setZoom(tempval);
								break;

							case "LTT1:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 5)
									tempval = 5;
								logicWindow.setCh1Trigger(tempval);
								break;

							case "LTT2:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 5)
									tempval = 5;
								logicWindow.setCh2Trigger(tempval);
								break;

							case "LTT3:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 5)
									tempval = 5;
								logicWindow.setCh3Trigger(tempval);
								break;

							case "LTTC:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 256)
									tempval = 256;
								if (tempval < 1)
									tempval = 1;
								logicWindow.setTrigCount(tempval);
								break;

							case "LTSR:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 7)
									tempval = 7;
								logicWindow.setSampleRate(tempval);
								break;

							case "LTTP:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 5)
									tempval = 5;
								logicWindow.setTriggerPosition(tempval);
								break;


							case "LTCE:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									logicWindow.setCursorsEnabled(true);
								}
								break;

							case "LTCX:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 4095)
									tempval = 4095;
								logicWindow.setXCursorPos(tempval);
								break;

							case "LTCY:":
								tempval = Int32.Parse(fileLine.Substring(6, (fileLine.Length - 6)));
								if (tempval > 4095)
									tempval = 4095;
								logicWindow.setYCursorPos(tempval);
								break;

							// Programmer-To-Go settings ------------------------------------------------
							// Programmer-To-Go settings ------------------------------------------------
							case "PTGM:":
								if (string.Compare(fileLine.Substring(6, 1), "1") == 0)
								{
									ptgMemory = 1;  // 256K bytes of I2C EEPROM =====
								}
								else if (string.Compare(fileLine.Substring(6, 1), "2") == 0)
								{
									ptgMemory = 2;  // 512K bytes of SPI Flash =====
								}
								else if (string.Compare(fileLine.Substring(6, 1), "3") == 0)
								{
									ptgMemory = 3;  // 1M bytes of SPI Flash =====
								}
								else if (string.Compare(fileLine.Substring(6, 1), "4") == 0)
								{
									ptgMemory = 4;  // 2M bytes of SPI Flash =====
								}
								else if (string.Compare(fileLine.Substring(6, 1), "5") == 0)
								{
									ptgMemory = 5;  // 4M bytes of SPI Flash =====
								}
								break;

							// Alert Sounds settings ------------------------------------------------ 
							case "SDSP:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									PlaySuccessWav = true;
								}
								break;

							case "SDWP:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									PlayWarningWav = true;
								}
								break;

							case "SDEP:":
								if (string.Compare(fileLine.Substring(6, 1), "Y") == 0)
								{
									PlayErrorWav = true;
								}
								break;

							case "SDSF:":
								SuccessWavFile = fileLine.Substring(6);
								break;

							case "SDWF:":
								WarningWavFile = fileLine.Substring(6);
								break;

							case "SDEF:":
								ErrorWavFile = fileLine.Substring(6);
								break;

							default:
								break;
						}

					}
					fileLine = hexRead.ReadLine();
				}
				hexRead.Close();
			}
			catch
			{
				return 0;
			}

			// add toolstrip menu items.
			hex1ToolStripMenuItem.Text = "&1 " + shortenHex(hex1);
			hex2ToolStripMenuItem.Text = "&2 " + shortenHex(hex2);
			hex3ToolStripMenuItem.Text = "&3 " + shortenHex(hex3);
			hex4ToolStripMenuItem.Text = "&4 " + shortenHex(hex4);

			return returnSETV;

		}

		private string shortenHex(string fullPath)
		{
			if (fullPath.Length > 42)
			{
				return (fullPath.Substring(0, 3) + "..." + fullPath.Substring(fullPath.Length - 36));
			}
			return fullPath;
		}

		private void hex1Click(object sender, EventArgs e)
		{
			hexImportFromHistory(hex1);
		}

		private void hex2Click(object sender, EventArgs e)
		{
			hexImportFromHistory(hex2);
		}

		private void hex3Click(object sender, EventArgs e)
		{
			hexImportFromHistory(hex3);
		}

		private void hex4Click(object sender, EventArgs e)
		{
			hexImportFromHistory(hex4);
		}

		private void hexImportFromHistory(string filename)
		{
			// if import disabled, do nothing.
			// if text blank, do nothing.
			if (importFileToolStripMenuItem.Enabled)
			{
				if (filename.Length > 3)
				{
					openHexFileDialog.FileName = filename;
					importHexFileGo();
					updateGUI(KONST.UpdateMemoryDisplays);
				}
			}
		}

		private void memorySelectVerify(object sender, EventArgs e)
		{
			if (sender.Equals(checkBoxProgMemEnabled))
				checkBoxProgMemEnabledAlt.Checked = checkBoxProgMemEnabled.Checked;
			if (sender.Equals(checkBoxProgMemEnabledAlt))
				checkBoxProgMemEnabled.Checked = checkBoxProgMemEnabledAlt.Checked;
			if (sender.Equals(checkBoxEEMem))
				checkBoxEEDATAMemoryEnabledAlt.Checked = checkBoxEEMem.Checked;
			if (sender.Equals(checkBoxEEDATAMemoryEnabledAlt))
				checkBoxEEMem.Checked = checkBoxEEDATAMemoryEnabledAlt.Checked;

			if (!checkBoxProgMemEnabled.Checked && !checkBoxEEMem.Checked)
			{ // if no memory regions are checked
				MessageBox.Show("At least one memory region\nmust be selected.");
				if ((sender.Equals(checkBoxProgMemEnabled)) || (sender.Equals(checkBoxProgMemEnabledAlt)))
				{
					checkBoxProgMemEnabled.Checked = true;
					checkBoxProgMemEnabledAlt.Checked = true;
				}
				else
				{
					checkBoxEEMem.Checked = true;
					checkBoxEEDATAMemoryEnabledAlt.Checked = true;
				}
			}

			updateGUI(KONST.DontUpdateMemDisplays);
		}

		private void setOSCCAL(object sender, EventArgs e)
		{
			if (setOSCCALToolStripMenuItem.Enabled)
			{ // get around menu bug
				SetOSCCAL osccalForm = new SetOSCCAL();
				osccalForm.ShowDialog();
				if (setOSCCALValue)
				{
					eraseDeviceAll(true, new uint[0]);
					displayStatusWindow.Text += "\nOSCCAL Set.";
				}
				setOSCCALValue = false;
				updateGUI(KONST.UpdateMemoryDisplays);
			}
		}

		private void pickit2OnTheWeb(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start("http://www.microchip.com/pickit2");
			}
			catch
			{
				MessageBox.Show("Unable to open link.");
			}
		}

		private void troubleshhotToolStripMenuItem_Click(object sender, EventArgs e)
		{
			DialogTroubleshoot tshootWindow = new DialogTroubleshoot();
			tshootWindow.ShowDialog();
			chkBoxVddOn.Checked = false;
			if (selfPoweredTarget)
			{
				Pk2.ForceTargetPowered();
			}
		}

		private void MCLRtoolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (MCLRtoolStripMenuItem.Checked)
			{
				checkBoxMCLR.Checked = false;
				MCLRtoolStripMenuItem.Checked = false;
				Pk2.HoldMCLR(false);
			}
			else
			{
				checkBoxMCLR.Checked = true;
				MCLRtoolStripMenuItem.Checked = true;
				Pk2.HoldMCLR(true);
			}
		}

		private void toolStripMenuItemTestMemory_Click(object sender, EventArgs e)
		{
			if (TestMemoryEnabled)
			{
				if (!TestMemoryOpen)
				{
					openTestMemory();
				}
			}
		}

		private void openTestMemory()
		{
			formTestMem = new FormTestMemory();
			formTestMem.UpdateMainFormGUI = new DelegateUpdateGUI(this.ExtCallUpdateGUI);
			formTestMem.CallMainFormEraseWrCal = new DelegateWriteCal(this.ExtCallCalEraseWrite);
			formTestMem.Show();
		}

		private void buttonImportWrite(object sender, EventArgs e)
		{
			// import hex file
			/* importGo = false;
			openHexFileDialog.ShowDialog();          
            
			if (importGo)
			{
				updateGUI(KONST.UpdateMemoryDisplays);
				this.Refresh();
				// Write the device
				deviceWrite();
			}*/
		}

		private void checkBoxAutoImportWrite_Click(object sender, EventArgs e)
		{

			if (!checkBoxAutoImportWrite.Checked)
			{
				displayStatusWindow.Text = "Exited Auto-Import-Write mode.";
			}
			if (checkBoxAutoImportWrite.Checked)
			{
				importGo = false;
				if (hex1.Length > 3)
				{
					openHexFileDialog.FileName = hex1; // defaults to first file in history.
				}
				openHexFileDialog.ShowDialog();
				if (importGo)
				{
					updateGUI(KONST.UpdateMemoryDisplays);
					this.Refresh();
					// Write the device
					if (deviceWrite())
					{
						importFileToolStripMenuItem.Enabled = false;
						exportFileToolStripMenuItem.Enabled = false;
						//programmerToolStripMenuItem.Enabled = false;
						readDeviceToolStripMenuItem.Enabled = false;
						writeDeviceToolStripMenuItem.Enabled = false;
						verifyToolStripMenuItem.Enabled = false;
						eraseToolStripMenuItem.Enabled = false;
						blankCheckToolStripMenuItem.Enabled = false;
						writeOnPICkitButtonToolStripMenuItem.Enabled = false;
						pICkit2GoToolStripMenuItem.Enabled = false;
						setOSCCALToolStripMenuItem.Enabled = false;
						buttonRead.Enabled = false;
						buttonWrite.Enabled = false;
						buttonVerify.Enabled = false;
						buttonErase.Enabled = false;
						buttonBlankCheck.Enabled = false;
						dataGridProgramMemory.Enabled = false;
						dataGridViewEEPROM.Enabled = false;
						buttonExportHex.Enabled = false;
						deviceToolStripMenuItem.Enabled = false;
						checkCommunicationToolStripMenuItem.Enabled = false;
						troubleshhotToolStripMenuItem.Enabled = false;
						downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;
						//this.Text = "Auto Import-Wr Mode";
						// The next three satements are needed to ensure that the taskbar text is updated.
						//this.WindowState = FormWindowState.Minimized;
						//Application.DoEvents();
						//this.WindowState = FormWindowState.Normal;
						displayStatusWindow.Text += "Waiting for file update...  (Click button again to exit)";
						timerAutoImportWrite.Enabled = true;
					}
					else
					{ // write fails.
						importGo = false;
					}
				}
				else
				{ // import fails
					updateGUI(KONST.UpdateMemoryDisplays);
				}

				// if any action fails during import/programming, it should pop out the button (uncheck).
				if (!importGo)
				{
					checkBoxAutoImportWrite.Checked = false;
				}
			}

		}

		private void checkBoxAutoImportWrite_Changed(object sender, EventArgs e)
		{

			if (!checkBoxAutoImportWrite.Checked || !importGo)
			{
				importFileToolStripMenuItem.Enabled = true;
				exportFileToolStripMenuItem.Enabled = true;
				//programmerToolStripMenuItem.Enabled = true;
				readDeviceToolStripMenuItem.Enabled = true;
				writeDeviceToolStripMenuItem.Enabled = true;
				verifyToolStripMenuItem.Enabled = true;
				eraseToolStripMenuItem.Enabled = true;
				blankCheckToolStripMenuItem.Enabled = true;
				writeOnPICkitButtonToolStripMenuItem.Enabled = true;
				setOSCCALToolStripMenuItem.Enabled = true;
				buttonRead.Enabled = true;
				buttonWrite.Enabled = true;
				buttonVerify.Enabled = true;
				buttonErase.Enabled = true;
				buttonBlankCheck.Enabled = true;
				dataGridProgramMemory.Enabled = true;
				dataGridViewEEPROM.Enabled = true;
				buttonExportHex.Enabled = true;
				deviceToolStripMenuItem.Enabled = true;
				checkCommunicationToolStripMenuItem.Enabled = true;
				troubleshhotToolStripMenuItem.Enabled = true;
				downloadPICkit2FirmwareToolStripMenuItem.Enabled = true;
				timerAutoImportWrite.Enabled = false;

				// currently not supported in the PICkit 3 GUI
				if (!Pk2.isPK3)
				{
					pICkit2GoToolStripMenuItem.Enabled = true;
				}

				// Flashes the taskbar
				// </summary>
				// <param name="hWnd">The handle to the window to flash</param>
				FLASHWINFO fInfo = new FLASHWINFO();

				fInfo.cbSize = (ushort)Marshal.SizeOf(fInfo);
				fInfo.hwnd = this.Handle;
				fInfo.dwFlags = KONST.FLASHW_TIMERNOFG | KONST.FLASHW_TRAY;
				fInfo.uCount = UInt16.MaxValue;
				fInfo.dwTimeout = 0;

				FlashWindowEx(ref fInfo);

				if (this.WindowState == FormWindowState.Minimized)
				{
					this.WindowState = FormWindowState.Normal;
				}

			}
		}

		private void timerAutoImportWrite_Tick(object sender, EventArgs e)
		{
			FileInfo hexFile = new FileInfo(openHexFileDialog.FileName);
			if (ImportExportHex.LastWriteTime != hexFile.LastWriteTime)
			{ // import and write if hex file updated.
				if (deviceWrite())
				{
					importFileToolStripMenuItem.Enabled = false;
					exportFileToolStripMenuItem.Enabled = false;
					//programmerToolStripMenuItem.Enabled = false;
					readDeviceToolStripMenuItem.Enabled = false;
					writeDeviceToolStripMenuItem.Enabled = false;
					verifyToolStripMenuItem.Enabled = false;
					eraseToolStripMenuItem.Enabled = false;
					blankCheckToolStripMenuItem.Enabled = false;
					writeOnPICkitButtonToolStripMenuItem.Enabled = false;
					pICkit2GoToolStripMenuItem.Enabled = false;
					setOSCCALToolStripMenuItem.Enabled = false;
					buttonRead.Enabled = false;
					buttonWrite.Enabled = false;
					buttonVerify.Enabled = false;
					buttonErase.Enabled = false;
					buttonBlankCheck.Enabled = false;
					dataGridProgramMemory.Enabled = false;
					dataGridViewEEPROM.Enabled = false;
					buttonExportHex.Enabled = false;
					deviceToolStripMenuItem.Enabled = false;
					checkCommunicationToolStripMenuItem.Enabled = false;
					troubleshhotToolStripMenuItem.Enabled = false;
					downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;
					displayStatusWindow.Text += "Waiting for file update...  (Click button again to exit)";
				}
				else
				{ // write fails.
					timerAutoImportWrite.Enabled = false;
					checkBoxAutoImportWrite.Checked = false;
				}
			}


		}

		private bool checkForTest()
		{
			string testFileName = "Pk2Test.dll";

			if (TestMemoryEnabled)
			{
				return File.Exists(testFileName);
			}

			return false;


		}

		private bool testMenuBuild()
		{
			return true;
		}

		private void testMenuDropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{


		}

		private void buttonShowIDMem_Click(object sender, EventArgs e)
		{
			if (!DialogUserIDs.IDMemOpen)
			{
				dialogIDMemory = new DialogUserIDs();
				dialogIDMemory.Show();
			}
		}

		private uint getEEBlank()
		{
			uint eeBlank = 0xFF;
			if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].EEMemAddressIncrement > 1)
			{
				eeBlank = 0xFFFF;
			}
			if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue == 0xFFF)
			{
				eeBlank = 0xFFF;
			}
			return eeBlank;
		}

		private void restoreVddTarget()
		{
			if (VddTargetSave == KONST.VddTargetSelect.auto)
			{
				vddAuto();
			}
			else if (VddTargetSave == KONST.VddTargetSelect.pickit2)
			{
				vddPk2();
			}
			else
			{
				vddTarget();
			}
		}

		private void VppFirstToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (VppFirstToolStripMenuItem.Checked)
			{
				if (toolStripMenuItemLVPEnabled.Checked)
				{
					string scriptname = Pk2.DevFile.Scripts[Pk2.DevFile.PartsList[Pk2.ActivePart].LVPScript - 1].ScriptName;
					scriptname = scriptname.Substring(scriptname.Length - 2);
					if (scriptname == "HV")
					{
						MessageBox.Show("'Use High Voltage Program Entry' is enabled.\n\nVPP First Program Entry may not be used\nwhile that option is enabled.", "Use VPP First Program Entry");
					}
					else
					{
						MessageBox.Show("'Use LVP Program Entry' is enabled.\n\nVPP First Program Entry may not be used\nwhile that option is enabled.", "Use VPP First Program Entry");
					}
					VppFirstToolStripMenuItem.Checked = false;
				}
				else
				{
					Pk2.SetVPPFirstProgramEntry();
					displayStatusWindow.Text =
						"VPP First programming mode entry set.\nTo use, " + Pk2.ToolName + " MUST supply VDD to target.";
					if (toolStripMenuItemManualSelect.Checked)
					{
						Pk2.PrepNewPart(false);
					}
					if (autoDetectToolStripMenuItem.Checked)
					{
						VddTargetSave = KONST.VddTargetSelect.auto;
					}
					else if (forcePICkit2ToolStripMenuItem.Checked)
					{
						VddTargetSave = KONST.VddTargetSelect.pickit2;
					}
					else
					{
						VddTargetSave = KONST.VddTargetSelect.target;
					}
					vddPk2();
					targetPowerToolStripMenuItem.Enabled = false;
				}
			}
			else
			{
				Pk2.ClearVppFirstProgramEntry();
				if (toolStripMenuItemManualSelect.Checked)
					Pk2.PrepNewPart(false);
				if (!toolStripMenuItemLVPEnabled.Checked)
					displayStatusWindow.Text = "Normal programming mode entry.";
				targetPowerToolStripMenuItem.Enabled = true;
				restoreVddTarget();
			}
		}

		private bool eepromWrite(bool eraseWrite)
		{

			if (!preProgrammingCheck(Pk2.GetActiveFamily()))
			{
				return false; // abort
			}

			updateGUI(KONST.DontUpdateMemDisplays);
			this.Update();

			if (checkImportFile && !eraseWrite)
			{
				FileInfo hexFile = new FileInfo(openHexFileDialog.FileName);
				if (ImportExportHex.LastWriteTime != hexFile.LastWriteTime)
				{
					displayStatusWindow.Text = "Reloading Hex File\n";
					//displayStatusWindow.Update();
					this.Update();
					Thread.Sleep(300);
					if (!importHexFileGo())
					{
						displayStatusWindow.Text = "Error Loading Hex File: Write aborted.\n";
						statusWindowColor = Constants.StatusColor.red;
						updateGUI(KONST.UpdateMemoryDisplays);
						return false;
					}
				}
			}

			Pk2.VddOn();

			if (eraseWrite)
			{
				displayStatusWindow.Text = "Erasing device:\n";
			}
			else
			{
				displayStatusWindow.Text = "Writing device:\n";
			}
			this.Update();

			int endOfBuffer = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem;

			// Write  Memory
			if (checkBoxProgMemEnabled.Checked)
			{
				Pk2.RunScript(KONST.PROG_ENTRY, 1);

				displayStatusWindow.Text += "EEPROM... ";
				this.Update();
				progressBar1.Value = 0;     // reset bar   

				int extraBytes = 3; // 3 extra bytes for address.
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
				{
					extraBytes = 4;
				}

				int wordsPerWrite = Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrWords;
				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation;
				int dataDownloadSize = KONST.DownLoadBufferSize;
				if (endOfBuffer < dataDownloadSize)
				{
					dataDownloadSize = endOfBuffer + (endOfBuffer / (wordsPerWrite * bytesPerWord)) * extraBytes;
				}
				if (dataDownloadSize > KONST.DownLoadBufferSize)
				{
					dataDownloadSize = KONST.DownLoadBufferSize;
				}

				int scriptRunsToUseDownload = dataDownloadSize /
					((wordsPerWrite * bytesPerWord) + extraBytes);
				int wordsPerLoop = scriptRunsToUseDownload * wordsPerWrite;
				int wordsWritten = 0;

				progressBar1.Maximum = (int)endOfBuffer / wordsPerLoop;

				byte[] downloadBuffer = new byte[KONST.DownLoadBufferSize];

				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrPrepScript != 0)
				{ // if prog mem write prep  script exists for this part
					Pk2.RunScript(KONST.PROGMEM_WR_PREP, 1);
				}

				do
				{
					int downloadIndex = 0;

					for (int word = 0; word < wordsPerLoop; word++)
					{
						if (wordsWritten == endOfBuffer)
						{
							// we may not have filled download buffer, so adjust number of script runs
							scriptRunsToUseDownload = downloadIndex / ((wordsPerWrite * bytesPerWord) + extraBytes);
							break; // for cases where ProgramMemSize%WordsPerLoop != 0
						}

						if ((wordsWritten % wordsPerWrite) == 0)
						{ // beginning of each script call
							// EEPROM address in download buffer
							int eeAddress = eeprom24BitAddress(wordsWritten, KONST.WRITE_BIT);
							if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
							{
								downloadBuffer[downloadIndex++] = 0x96; //WREN
							}
							downloadBuffer[downloadIndex++] = (byte)((eeAddress >> 16) & 0xFF); // upper byte
							downloadBuffer[downloadIndex++] = (byte)((eeAddress >> 8) & 0xFF); // high byte
							downloadBuffer[downloadIndex++] = (byte)(eeAddress & 0xFF); // low byte  
						}

						uint memWord = Pk2.DeviceBuffers.ProgramMemory[wordsWritten++];

						downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);

						for (int bite = 1; bite < bytesPerWord; bite++)
						{
							memWord >>= 8;
							downloadBuffer[downloadIndex++] = (byte)(memWord & 0xFF);
						}

						if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.MICROWIRE_BUS)
							 && (bytesPerWord == 2))
						{ // "Endian-ness" of Microwire 16-bit words need to be swapped
							byte swapTemp = downloadBuffer[downloadIndex - 2];
							downloadBuffer[downloadIndex - 2] = downloadBuffer[downloadIndex - 1];
							downloadBuffer[downloadIndex - 1] = swapTemp;
						}

					}
					// download data
					int dataIndex = Pk2.DataClrAndDownload(downloadBuffer, 0);
					while (dataIndex < downloadIndex)
					{
						dataIndex = Pk2.DataDownload(downloadBuffer, dataIndex, downloadIndex);
					}

					Pk2.RunScript(KONST.PROGMEM_WR, scriptRunsToUseDownload);

					if ((Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
						|| (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS))
					{
						if (eeprom_CheckBusErrors())
						{
							return false;
						}
					}
					progressBar1.PerformStep();
				} while (wordsWritten < endOfBuffer);

			}

			Pk2.RunScript(KONST.PROG_EXIT, 1);

			//Verify
			bool verifySuccess = true;

			if (verifyOnWriteToolStripMenuItem.Checked && !eraseWrite)
			{
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
					conditionalVDDOff(); // POR for UNIO
				verifySuccess = deviceVerify(true, (endOfBuffer - 1), false);
			}

			conditionalVDDOff();

			if (verifySuccess && !eraseWrite)
			{
				statusWindowColor = Constants.StatusColor.green;
				displayStatusWindow.Text = "Programming Successful.\n";

				updateGUI(KONST.UpdateMemoryDisplays);

				return true;

			}
			return verifySuccess;

		}

		private int eeprom24BitAddress(int wordsWritten, bool setReadBit)
		{
			if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.I2C_BUS)
			{
				int tempAddress = wordsWritten;
				int address = 0;
				int chipSelects = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.CS_PINS_CFG];

				// I2C
				// Low & mid bytes
				address = wordsWritten & Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] & 0xFFFF;
				// block address
				tempAddress >>= (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_BITS_CFG]);
				tempAddress <<= 17 + chipSelects; // 2 words plus R/W bit
				if (chipSelects > 0)
				{
					if (checkBoxA0CS.Checked)
					{
						tempAddress |= 0x00020000;
					}
					if (checkBoxA1CS.Checked)
					{
						tempAddress |= 0x00040000;
					}
					if (checkBoxA2CS.Checked)
					{
						tempAddress |= 0x00080000;
					}
				}

				address += (tempAddress & 0x000E0000) + 0x00A00000;
				if (setReadBit)
				{
					address |= 0x00010000;
				}

				return address;
			}
			else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.SPI_BUS)
			{
				int tempAddress = wordsWritten;
				int address = 0;

				//SPI
				// Low & Mid bytes
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem <= 0x10000)
				{
					address = wordsWritten & Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] & 0xFFFF;
					tempAddress >>= (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_BITS_CFG]);
					tempAddress <<= 19; // 2 words plus 3 instruction bits
					address += (tempAddress & 0x00080000) + 0x00020000; // write instruction
					if (setReadBit)
					{
						address |= 0x00010000; // add read instruction bit.
					}
				}
				else
				{
					address = wordsWritten;
				}

				return address;
			}
			else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.MICROWIRE_BUS)
			{
				int tempAddress = 0x05; // start bit and write opcode
				int address = 0;

				// Microwire
				// Low & mid bytes
				address = wordsWritten & Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] & 0xFFFF;
				if (setReadBit)
				{
					tempAddress = 0x06; // start bit and read opcode
				}
				tempAddress <<= (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_BITS_CFG]);

				address |= tempAddress;

				return address;
			}
			else if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
			{
				int address = 0;

				address = wordsWritten & Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.ADR_MASK_CFG] & 0xFFFF;

				if (setReadBit)
				{
					address |= 0x030000; // READ command
				}
				else
				{
					address |= 0x6C0000; // WRITE command
				}
				return address;
			}
			return 0;
		}

		private bool eeprom_CheckBusErrors()
		{
			if (Pk2.BusErrorCheck())
			{
				Pk2.RunScript(KONST.PROG_EXIT, 1);
				conditionalVDDOff();
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[KONST.PROTOCOL_CFG] == KONST.UNIO_BUS)
				{
					displayStatusWindow.Text = "UNI/O Bus Error (NoSAK) - Aborted.\n";
				}
				else
				{
					displayStatusWindow.Text = "I2C Bus Error (No Acknowledge) - Aborted.\n";
				}
				statusWindowColor = Constants.StatusColor.yellow;
				updateGUI(KONST.UpdateMemoryDisplays);
				return true;
			}
			return false; // no errors
		}

		private void calibrateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			DialogCalibrate calWindow = new DialogCalibrate();
			calWindow.ShowDialog();
			chkBoxVddOn.Checked = false;
			if (selfPoweredTarget)
			{
				Pk2.ForceTargetPowered();
			}
			detectPICkit2(KONST.ShowMessage, true);
		}

		private void UARTtoolStripMenuItem_Click(object sender, EventArgs e)
		{
			/*            if (chkBoxVddOn.Checked)
						{
							DialogResult buttonPressed = MessageBox.Show("When using the UART Tool,\nPICkit 2 cannot supply VDD.\n\nClick the OK button to turn\noff VDD and continue.",
											"PICkit 2 UART Tool", MessageBoxButtons.OKCancel);
							if (buttonPressed == DialogResult.Cancel)
							{
								return;
							}

							chkBoxVddOn.Checked = false;
						}*/
			timerButton.Enabled = false;            // don't poll for button in UART mode!
			MCLRtoolStripMenuItem.Checked = false;
			checkBoxMCLR.Checked = false;
			//Pk2.VddOff();
			//Pk2.ForceTargetPowered();

			uartWindow.SetVddBox(numUpDnVDD.Enabled, chkBoxVddOn.Checked);

			if (multiWindow)
			{
				programMemMultiWin.Hide();
				eepromDataMultiWin.Hide();
			}
			this.Hide();
			uartWindow.ShowDialog();
			this.Show();
			if (multiWindow)
			{
				if (multiWinPMemOpen)
				{
					programMemMultiWin.Show();
				}
				if ((multiWinEEMemOpen) && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
				{
					eepromDataMultiWin.Show();
				}
				this.Focus();
			}

			if (!selfPoweredTarget)
			{
				Pk2.ForcePICkitPowered();
			}
			if (writeOnPICkitButtonToolStripMenuItem.Checked)
			{
				buttonLast = true;
				timerButton.Enabled = true;
			}
		}


		private void toolStripMenuItemSingleWindow_Click(object sender, EventArgs e)
		{
			if (multiWindow)
				viewChanged = true;
			multiWindow = false;
			updateGUI(true);
		}

		private void toolStripMenuItemMultiWindow_Click(object sender, EventArgs e)
		{
			if (!multiWindow)
				viewChanged = true;
			multiWindow = true;
			updateGUI(true);
		}

		private void toolStripMenuItemShowProgramMemory_Click(object sender, EventArgs e)
		{
			if (multiWinPMemOpen)
			{
				multiWinPMemOpen = false;
				toolStripMenuItemShowProgramMemory.Checked = false;
				programMemMultiWin.Hide();
			}
			else
			{
				multiWinPMemOpen = true;
				toolStripMenuItemShowProgramMemory.Checked = true;
				programMemMultiWin.Show();
			}
			this.Focus();
		}

		private void toolStripMenuItemShowEEPROMData_Click(object sender, EventArgs e)
		{
			if (multiWinEEMemOpen)
			{
				multiWinEEMemOpen = false;
				toolStripMenuItemShowEEPROMData.Checked = false;
				eepromDataMultiWin.Hide();
			}
			else
			{
				multiWinEEMemOpen = true;
				toolStripMenuItemShowEEPROMData.Checked = true;
				eepromDataMultiWin.Show();
			}
			this.Focus();
		}

		private void FormPICkit2_Move(object sender, EventArgs e)
		{
			if (this.WindowState != FormWindowState.Minimized)
			{
				if (multiWindow && mainWinOwnsMem)
				{
					int deltaX = this.Location.X - lastLoc.X;
					int deltaY = this.Location.Y - lastLoc.Y;

					// get desktop size:
					int desktopHeigth = SystemInformation.VirtualScreen.Height;
					int desktopWidth = SystemInformation.VirtualScreen.Width;

					// move progmem window with us
					int newX = programMemMultiWin.Location.X + deltaX;
					int newY = programMemMultiWin.Location.Y + deltaY;
					//unless it would move it offscreen
					if ((newX + 75) > desktopWidth)
						newX = desktopWidth - 75;
					if (newX < 0)
						newX = 0;
					if ((newY + 75) > desktopHeigth)
						newY = desktopHeigth - 75;
					if (newY < 0)
						newY = 0;

					if ((programMemMultiWin.WindowState != FormWindowState.Maximized) && (programMemMultiWin.WindowState != FormWindowState.Minimized))
						programMemMultiWin.Location = new Point(newX, newY);

					// move eeprom window with us
					newX = eepromDataMultiWin.Location.X + deltaX;
					newY = eepromDataMultiWin.Location.Y + deltaY;
					//unless it would move it offscreen
					if ((newX + 75) > desktopWidth)
						newX = desktopWidth - 75;
					if (newX < 0)
						newX = 0;
					if ((newY + 75) > desktopHeigth)
						newY = desktopHeigth - 75;
					if (newY < 0)
						newY = 0;

					if ((eepromDataMultiWin.WindowState != FormWindowState.Maximized) && (eepromDataMultiWin.WindowState != FormWindowState.Minimized))
						eepromDataMultiWin.Location = new Point(newX, newY);
				}
				lastLoc = this.Location;
			}
		}

		private void pICkit2GoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			// CHECK FOR SUPPORTED FAMILIES
			if (Pk2.FamilyIsEEPROM() || Pk2.FamilyIsKeeloq() || Pk2.FamilyIsPIC32() || Pk2.FamilyIsMCP())
			{
				MessageBox.Show("PICkit 2 Programmer-To-Go does not support\n" + Pk2.DevFile.Families[Pk2.GetActiveFamily()].FamilyName + " family devices.", "Unsupported Device Family");
				return;
			}
			// CHECK FOR VALID DEVICE
			if (Pk2.ActivePart == 0)
			{
				MessageBox.Show("No device selected!", "Programmer-To-Go");
				return;
			}
			// CHECK FOR DEVICES REQUIRING READ/RESTORE OF EEPROM
			if (!checkBoxEEMem.Checked && checkBoxEEMem.Enabled)
			{
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemEraseScript == 0)
				{
					MessageBox.Show("PICkit 2 Programmer-To-Go does not support\npreserving EEPROM on devices requiring a\nRead/Restore operation.\n\nThe entire device must be programmed.", "Programmer-To-Go");
					return;
				}
			}

			// CHECK TO SEE IF PROGRAM FITS IN PICKIT 2
			if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0x3FFF)
			{ // midrange & baseline aren't an issue
				int bytesPerWord = Pk2.DevFile.Families[Pk2.GetActiveFamily()].BytesPerLocation;
				int configLocation = (int)Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigAddr /
					Pk2.DevFile.Families[Pk2.GetActiveFamily()].ProgMemHexBytes;
				int configWords = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigWords;
				int endOfBuffer = Pk2.DeviceBuffers.ProgramMemory.Length;
				if ((configLocation < Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem) && (configWords > 0))
				{// if config in program memory, set back end of buffer
					endOfBuffer -= (configWords + 1);

				}
				int wordsPerWrite = Pk2.DevFile.PartsList[Pk2.ActivePart].ProgMemWrWords;
				int scriptRunsToUseDownload = KONST.DownLoadBufferSize /
							(wordsPerWrite * bytesPerWord);
				int wordsPerLoop = scriptRunsToUseDownload * wordsPerWrite;
				endOfBuffer = Pk2.FindLastUsedInBuffer(Pk2.DeviceBuffers.ProgramMemory,
											Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue, (endOfBuffer - 1));
				// align end on next loop boundary                 
				int writes = (endOfBuffer + 1) / wordsPerLoop;
				if (((endOfBuffer + 1) % wordsPerLoop) > 0)
				{
					writes++;
				}
				endOfBuffer = writes * wordsPerLoop;

				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue > 0xFFFF)
				{ // 16-bit devices.
					float sizeFactor = 1.23F;
					if (Pk2.FamilyIsdsPIC30())
						sizeFactor = 1.26F;
					endOfBuffer = (int)(endOfBuffer * sizeFactor);
				}
				else
				{ //  PIC18
					float sizeFactor = 1.22F;
					if (Pk2.FamilyIsPIC18J())
						sizeFactor = 1.17F;
					endOfBuffer = (int)(endOfBuffer * sizeFactor);
				}
				endOfBuffer *= bytesPerWord;
				int memMax = 131072; // 128K
				if (ptgMemory >= 1)
					memMax = 131072 * (2 << (ptgMemory - 1)); // 256K and above memory limits ===== 

				if (endOfBuffer > memMax)
				{
					if (ptgMemory > 0)
						MessageBox.Show("The data in the buffer is too large\nto be downloaded to PICkit 2.\n\nIt cannot be used with Programmer-To-Go.", "Programmer-To-Go");
					else
						MessageBox.Show("The data in the buffer is too large\nto be downloaded to PICkit 2.\n\nSee section 3.1 of the Programmer-To-Go\nUser Guide for information on increasing\nthe PICkit 2 memory.", "Programmer-To-Go");
					return;
				}
			}
			// CHECK FOR VPPFIRST & VALID VDD
			if (VppFirstToolStripMenuItem.Checked && VppFirstToolStripMenuItem.Enabled)
			{
				if (((float)numUpDnVDD.Value < Pk2.DevFile.PartsList[Pk2.ActivePart].VddErase)
						&& (Pk2.DevFile.PartsList[Pk2.ActivePart].DebugRowEraseScript == 0))
				{
					MessageBox.Show("VPP First Program Entry selected:\n" + Pk2.ToolName + " must power target.\n\nHowever, VDD box setpoint is below the\nminimum Erase voltage for this part.", "Programmer-To-Go");
					return;
				}

			}

			// CHECKS PASSED ---------------------------------------------------------------

			// if (chkBoxVddOn.Checked)
			timerButton.Enabled = false;            // don't poll for button in PK2Go mode!
			//MCLRtoolStripMenuItem.Checked = false;

			// Send VDD value
			DialogPK2Go PK2GoWizard = new DialogPK2Go();
			PK2GoWizard.VDDVolts = (float)numUpDnVDD.Value;

			// Send Data Source
			if (multiWindow)
				PK2GoWizard.dataSource = shortenHex(displayDataSource.Text);
			else
				PK2GoWizard.dataSource = displayDataSource.Text;

			// Send CP & DP settings
			if ((enableCodeProtectToolStripMenuItem.Checked) || (enableDataProtectStripMenuItem.Checked))
			{
				if (enableDataProtectStripMenuItem.Checked)
				{
					PK2GoWizard.dataProtect = true;
				}
				if (enableCodeProtectToolStripMenuItem.Checked)
				{
					PK2GoWizard.codeProtect = true;
				}
			}
			// Send Memory Regions
			PK2GoWizard.writeProgMem = checkBoxProgMemEnabled.Checked;
			PK2GoWizard.writeEEPROM = checkBoxEEMem.Checked;
			// Send verify
			if (verifyOnWriteToolStripMenuItem.Checked)
				PK2GoWizard.verifyDevice = true;
			if (VppFirstToolStripMenuItem.Enabled && VppFirstToolStripMenuItem.Checked)
				PK2GoWizard.vppFirst = true;
			// send fast programming
			if (fastProgrammingToolStripMenuItem.Enabled && !fastProgrammingToolStripMenuItem.Checked)
				PK2GoWizard.fastProgramming = false;
			PK2GoWizard.icspSpeedSlow = slowSpeedICSP;
			// send MCLR hold
			if (MCLRtoolStripMenuItem.Enabled && MCLRtoolStripMenuItem.Checked)
				PK2GoWizard.holdMCLR = true;

			// Send PK2 mem size
			PK2GoWizard.SetPTGMemory(ptgMemory);


			PK2GoWizard.PICkit2WriteGo = new DelegateWrite(this.ExtCallWrite);
			PK2GoWizard.OpenProgToGoGuide = new DelegateOpenProgToGoGuide(this.OpenProgToGoUserGuide);

			// PTG doesn't support using PEs with 16-bit devices
			bool pe33save = usePE33;
			usePE33 = false;
			bool pe24fsave = usePE24;
			usePE24 = false;

			PK2GoWizard.ShowDialog();

			// restore PE settings
			usePE33 = pe33save;
			usePE24 = pe24fsave;

			// reset power
			if (!selfPoweredTarget)
			{
				Pk2.ForcePICkitPowered();
			}
			else
			{
				Pk2.ForcePICkitPowered();
			}

			if (writeOnPICkitButtonToolStripMenuItem.Checked)
			{
				buttonLast = true;
				timerButton.Enabled = true;
			}
		}

		private void toolStripMenuItemManualSelect_Click(object sender, EventArgs e)
		{
			ManualAutoSelectToggle(true);
		}

		private void ManualAutoSelectToggle(bool updateGUI_OK)
		{
			if (toolStripMenuItemManualSelect.Checked)
			{
				// Set all families to manual select
				for (int i = 0; i < Pk2.DevFile.Families.Length; i++)
				{
					Pk2.DevFile.Families[i].PartDetect = false;
				}
			}
			else
			{
				// Reset families to auto select
				for (int i = 0; i < Pk2.DevFile.Families.Length; i++)
				{
					if (Pk2.DevFile.Families[i].DeviceIDMask > 0)
					{
						Pk2.DevFile.Families[i].PartDetect = true;
					}
				}
				toolStripMenuItemLVPEnabled.Checked = false; // not allowed when auto detect enabled
			}
			FamilySelectLogic(Pk2.GetActiveFamily(), updateGUI_OK);
		}

		private void toolStripMenuItemProgToGo_Click(object sender, EventArgs e)
		{
			OpenProgToGoUserGuide();
		}

		public void OpenProgToGoUserGuide()
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + "\\Programmer-To-Go User Guide.pdf");
			}
			catch
			{
				MessageBox.Show("Unable to open Programmer-To-Go Guide.");
			}
		}

		private void toolStripMenuItemLogicTool_Click(object sender, EventArgs e)
		{
			timerButton.Enabled = false;            // don't poll for button in Logic mode!
			MCLRtoolStripMenuItem.Checked = false;
			checkBoxMCLR.Checked = false;

			logicWindow.SetVddBox(numUpDnVDD.Enabled, chkBoxVddOn.Checked);

			if (multiWindow)
			{
				programMemMultiWin.Hide();
				eepromDataMultiWin.Hide();
			}
			this.Hide();
			logicWindow.ShowDialog();
			this.Show();
			if (multiWindow)
			{
				if (multiWinPMemOpen)
				{
					programMemMultiWin.Show();
				}
				if ((multiWinEEMemOpen) && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
				{
					eepromDataMultiWin.Show();
				}
				this.Focus();
			}
			if (writeOnPICkitButtonToolStripMenuItem.Checked)
			{
				buttonLast = true;
				timerButton.Enabled = true;
			}
		}

		private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			if (e.ClickedItem.Text == "Select All")
			{
				if (dataGridProgramMemory.ContainsFocus)
				{
					dataGridProgramMemory.SelectAll();
				}
				else if (dataGridViewEEPROM.ContainsFocus)
				{
					dataGridViewEEPROM.SelectAll();
				}
			}
			else if (e.ClickedItem.Text == "Copy")
			{
				if (dataGridProgramMemory.ContainsFocus)
				{
					Clipboard.SetDataObject(this.dataGridProgramMemory.GetClipboardContent());
				}
				else if (dataGridViewEEPROM.ContainsFocus)
				{
					Clipboard.SetDataObject(this.dataGridViewEEPROM.GetClipboardContent());
				}
			}
		}

		private void dataGridProgramMemory_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
				dataGridProgramMemory.Focus();
		}

		private void dataGridViewEEPROM_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
				dataGridViewEEPROM.Focus();
		}

		private void toolStripMenuItemLogicToolUG_Click(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + "\\Logic Tool User Guide.pdf");
			}
			catch
			{
				MessageBox.Show("Unable to open Logic Tool User Guide.");
			}
		}

		private void calAutoRegenerateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (setOSCCALToolStripMenuItem.Enabled)
			{ // seems to be a .NET bug that can allow submenu access even when main menu is disabled.
				if (MessageBox.Show("Regenerating the OSCCAL value\nwill completely erase this\npart.\n\nAre you sure you wish to\ncontinue?",
					"Regenerate OSCCAL", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
				{
					return; // user cancelled
				}

				if (Pk2.DevFile.Families[Pk2.GetActiveFamily()].BlankValue == 0xFFF)
				{ // baseline

					short calValue = 0;
					float pulseWidth = 0;
					verifyOSCCALValue = false; // don't check during this procedure

					for (int iteration = 0; iteration < 5; iteration++) // try up to five times to cal
					{
						float calAdjust = ((1 - (400 / pulseWidth)) / 0.0057F) + 0.5F;
						calValue += (short)calAdjust;
						if ((calValue < -64) || (calValue > 63))
						{
							conditionalVDDOff();
							eraseDeviceAll(false, new uint[0]);
							verifyOSCCALValue = true;
							updateGUI(true);
							MessageBox.Show("Regenerating OSCCAL Failed!\n\nCalibration out of range.", "Regenerate OSCCAL");
							return;
						}

						// first, program part
						Pk2.ResetBuffers();
						Pk2.DeviceBuffers.ProgramMemory[0] = (KONST.BASELINE_CAL[0] | (((uint)calValue << 1) & 0xFF));
						Pk2.DeviceBuffers.ConfigWords[0] = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[6];
						for (int word = 1; word < KONST.BASELINE_CAL.Length; word++)
						{
							Pk2.DeviceBuffers.ProgramMemory[word] = KONST.BASELINE_CAL[word];
						}
						if (!deviceWrite())
						{
							Pk2.ResetBuffers();
							verifyOSCCALValue = true;
							updateGUI(true);
							MessageBox.Show("Regenerating OSCCAL Failed!\n\nUnable to program part.", "Regenerate OSCCAL");
							return;
						}

						Pk2.VddOn();

						for (int retry = 0; retry < 3; retry++)
						{
							pulseWidth = Pk2.MeasurePGDPulse();
							if ((pulseWidth < 695F) && (pulseWidth > 10F))
								break; // call it valid
							if (retry == 2)
							{
								conditionalVDDOff();
								eraseDeviceAll(false, new uint[0]);
								verifyOSCCALValue = true;
								MessageBox.Show("Regenerating OSCCAL Failed!\n\nUnable to connect to\ncalibration executive.", "Regenerate OSCCAL");
								updateGUI(true);
								return;
							}
						}

						conditionalVDDOff();

						float highlimit = 404F;
						if (iteration == 4)
							highlimit = 406F; // widen a bit for last
						float lowlimit = 396F;
						if (iteration == 4)
							highlimit = 394F; // widen a bit for last

						if ((pulseWidth > lowlimit) && (pulseWidth < highlimit))
						{ // success
							Pk2.DeviceBuffers.OSCCAL = Pk2.DeviceBuffers.ProgramMemory[0];
							eraseDeviceAll(true, new uint[0]);
							verifyOSCCALValue = true; // re-enable
							MessageBox.Show("Success!\n\nOSSCAL Regenerated and\nwritten to device.", "Regenerate OSCCAL");
							updateGUI(true);
							return;
						}
					}
				}
				else
				{ // midrange
					short calValue = 32;
					float pulseWidth = 0;
					verifyOSCCALValue = false; // don't check during this procedure

					for (int iteration = 0; iteration < 5; iteration++) // try up to five times to cal
					{
						float calAdjust = ((1 - (400 / pulseWidth)) / 0.007F) + 0.5F;
						calValue += (short)calAdjust;
						if ((calValue < 0) || (calValue > 63))
						{
							conditionalVDDOff();
							eraseDeviceAll(false, new uint[0]);
							verifyOSCCALValue = true;
							updateGUI(true);
							MessageBox.Show("Regenerating OSCCAL Failed!\n\nCalibration out of range.", "Regenerate OSCCAL");
							return;
						}

						// first, program part
						Pk2.ResetBuffers();
						Pk2.DeviceBuffers.ProgramMemory[0] = (KONST.MR16F676FAM_CAL[0] | (((uint)calValue << 2) & 0xFF));
						Pk2.DeviceBuffers.ConfigWords[0] = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[6];
						for (int word = 1; word < KONST.MR16F676FAM_CAL.Length; word++)
						{
							Pk2.DeviceBuffers.ProgramMemory[word] = KONST.MR16F676FAM_CAL[word];
						}
						if (!deviceWrite())
						{
							Pk2.ResetBuffers();
							verifyOSCCALValue = true;
							updateGUI(true);
							MessageBox.Show("Regenerating OSCCAL Failed!\n\nUnable to program part.", "Regenerate OSCCAL");
							return;
						}

						Pk2.VddOn();

						for (int retry = 0; retry < 3; retry++)
						{
							pulseWidth = Pk2.MeasurePGDPulse();
							if ((pulseWidth < 695F) && (pulseWidth > 10F))
								break; // call it valid
							if (retry == 2)
							{
								conditionalVDDOff();
								eraseDeviceAll(false, new uint[0]);
								verifyOSCCALValue = true;
								MessageBox.Show("Regenerating OSCCAL Failed!\n\nUnable to connect to\ncalibration executive.", "Regenerate OSCCAL");
								updateGUI(true);
								return;
							}
						}

						conditionalVDDOff();

						float highlimit = 404F;
						if (iteration == 4)
							highlimit = 406F; // widen a bit for last
						float lowlimit = 396F;
						if (iteration == 4)
							highlimit = 394F; // widen a bit for last

						if ((pulseWidth > lowlimit) && (pulseWidth < highlimit))
						{ // success
							Pk2.DeviceBuffers.OSCCAL = Pk2.DevFile.PartsList[Pk2.ActivePart].ConfigMasks[7] | (Pk2.DeviceBuffers.ProgramMemory[0] & 0xFF);
							eraseDeviceAll(true, new uint[0]);
							verifyOSCCALValue = true; // re-enable
							MessageBox.Show("Success!\n\nOSSCAL Regenerated and\nwritten to device.", "Regenerate OSCCAL");
							updateGUI(true);
							return;
						}
					}
				}
				eraseDeviceAll(false, new uint[0]);
				verifyOSCCALValue = true; // re-enable
				updateGUI(true);
				MessageBox.Show("Regenerating OSCCAL Failed!\n\nUnable to calibrate.", "Regenerate OSCCAL");
			}
		}

		private void timerInitalUpdate_Tick(object sender, EventArgs e)
		{
			timerInitalUpdate.Enabled = false;
			// wait to update GUI after main form displays or multi-monitor windowing sofware may
			// resize the first thing opened, which might be Program Memory or EEPROM Data windows
			// in multi-win view.
			toolStripMenuItemShowProgramMemory.Checked = saveMultWinPMemOpen;
			multiWinPMemOpen = saveMultWinPMemOpen;
			if (multiWinPMemOpen)
			{
				programMemMultiWin.Show();
			}
			toolStripMenuItemShowEEPROMData.Checked = saveMultiWinEEMemOpen;
			multiWinEEMemOpen = saveMultiWinEEMemOpen;
			if ((multiWinEEMemOpen) && (Pk2.DevFile.PartsList[Pk2.ActivePart].EEMem > 0))
				eepromDataMultiWin.Show();
			this.Focus();
		}

		private void mainWindowAlwaysInFrontToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (mainWindowAlwaysInFrontToolStripMenuItem.Checked)
			{
				mainWinOwnsMem = true;
				this.AddOwnedForm(programMemMultiWin);
				this.AddOwnedForm(eepromDataMultiWin);
			}
			else
			{
				mainWinOwnsMem = false;
				this.RemoveOwnedForm(programMemMultiWin);
				this.RemoveOwnedForm(eepromDataMultiWin);
			}
		}

		private bool useProgExec33()
		{
			if (Pk2.FamilyIsdsPIC33F() || Pk2.FamilyIsPIC24H())
			{
				if (Pk2.DevFile.PartsList[Pk2.ActivePart].ProgramMem >= 4096)
					// don't use PE on the smallest parts.
					return usePE33;
			}

			return false;
		}

		private bool useProgExec24F()
		{
			if (Pk2.FamilyIsPIC24FJ())
				return usePE24;

			return false;
		}

		private void updateAlertSoundCheck()
		{
			toolStripMenuItemSounds.Checked = PlayErrorWav || PlaySuccessWav || PlayWarningWav;
		}

		private void toolStripMenuItemSounds_Click(object sender, EventArgs e)
		{
			dialogSounds selectAlertSounds = new dialogSounds();
			selectAlertSounds.ShowDialog();
			updateAlertSoundCheck();
		}

		private void as0BitValueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			as0BitValueToolStripMenuItem.Checked = true;
			as1BitValueToolStripMenuItem.Checked = false;
			asReadOrImportedToolStripMenuItem.Checked = false;

			updateGUI(true);
		}

		private void as1BitValueToolStripMenuItem_Click(object sender, EventArgs e)
		{
			as0BitValueToolStripMenuItem.Checked = false;
			as1BitValueToolStripMenuItem.Checked = true;
			asReadOrImportedToolStripMenuItem.Checked = false;

			updateGUI(true);
		}

		private void asReadOrImportedToolStripMenuItem_Click(object sender, EventArgs e)
		{
			as0BitValueToolStripMenuItem.Checked = false;
			as1BitValueToolStripMenuItem.Checked = false;
			asReadOrImportedToolStripMenuItem.Checked = true;

			updateGUI(true);
		}

		private void labelConfig_Click(object sender, EventArgs e)
		{
			DialogConfigEdit configEditor = new DialogConfigEdit();
			configEditor.ScalefactW = ScalefactW;
			configEditor.ScalefactH = ScalefactH;
			if (as0BitValueToolStripMenuItem.Checked)
				configEditor.SetDisplayMask(0);
			else if (as1BitValueToolStripMenuItem.Checked)
				configEditor.SetDisplayMask(1);
			else
				configEditor.SetDisplayMask(2);
			configEditor.ShowDialog();

			if (ConfigsEdited)
			{
				displayDataSource.Text = "Edited.";
				checkImportFile = false;
				ConfigsEdited = false;
			}

			// display any changes.
			updateGUI(true);
		}

		private void toolStripMenuItemLVPEnabled_CheckedChanged(object sender, EventArgs e)
		{
			if (toolStripMenuItemLVPEnabled.Checked)
			{
				if (!toolStripMenuItemManualSelect.Checked)
				{
					string lvpScriptName = Pk2.DevFile.Scripts[Pk2.DevFile.PartsList[Pk2.ActivePart].LVPScript - 1].ScriptName;
					lvpScriptName = lvpScriptName.Substring(lvpScriptName.Length - 2);
					if (lvpScriptName == "HV")
					{
						MessageBox.Show("High Voltage Program entry may not be used\nwhen auto-detecting parts.\n\nSelect 'Programmer > Manual Device Select'\nto allow HVP to be used.", "Use HVP Program Entry");
					}
					else
					{
						MessageBox.Show("Low Voltage Program entry may not be used\nwhen auto-detecting parts.\n\nSelect 'Programmer > Manual Device Select'\nto allow LVP to be used.", "Use LVP Program Entry");
					}
					toolStripMenuItemLVPEnabled.Checked = false;
				}
				else
				{
					if (VppFirstToolStripMenuItem.Checked)
					{
						MessageBox.Show("'Use VPP First Program Entry' is enabled.\n\nLVP Program Entry may not be used while\nthat option is enabled.", "Use LVP Program Entry");
						toolStripMenuItemLVPEnabled.Checked = false;
					}
					else
					{
						Pk2.SetLVPProgramEntry();
						string lvpScriptName = Pk2.DevFile.Scripts[Pk2.DevFile.PartsList[Pk2.ActivePart].LVPScript - 1].ScriptName;
						if (lvpScriptName.Substring(lvpScriptName.Length - 2) == "HV")
						{
							displayStatusWindow.Text = "High Voltage Programming (HVP) entry set.";
						}
						else
						{
							displayStatusWindow.Text = "Low Voltage Programming (LVP) entry set.";
						}
						lvpScriptName = lvpScriptName.Substring(lvpScriptName.Length - 3);
						if (lvpScriptName == "PGM")
						{ // 200K devices use MCHP code sequence, not PGM pin - EXCEPT 18F K-series
							displayStatusWindow.Text += "\nConnect " + Pk2.ToolName + " AUX to device PGM pin.";
						}
						Pk2.PrepNewPart(false);
					}
				}
			}
			else
			{
				Pk2.ClearLVPProgramEntry();
				if (!VppFirstToolStripMenuItem.Checked)
					displayStatusWindow.Text = "Normal programming mode entry.";
				if (toolStripMenuItemManualSelect.Checked)
					Pk2.PrepNewPart(false);
			}
			updateGUI(false);
		}

		private void revertToMPLABModeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (!Pk2.TestPk3BootloaderExists())
			{
				displayStatusWindow.Text = "No bootloader found on PICkit 3.\nReverting to MPLAB mode aborted.";
				displayStatusWindow.BackColor = Color.Salmon;
				return;
			}

			if (MessageBox.Show("Clicking Ok will program the PICkit 3 so that it will restart into bootloader mode. This allows communication\n" +
							   "with MPLAB software and hence the ability to properly communicate with MPLAB\n\n" +

							   "Note: this mode can be alternatively activated by holding down the PICkit 3 button during USB plug-in,\n" +
							   "however, in this case, no change is made to the PICkit 3's programmed image.\n",
							   "Revert to MPLAB mode (Bootloader)?", MessageBoxButtons.OKCancel)
					== DialogResult.Cancel)
				return;

			// disable write on button
			timerButton.Enabled = false;
			writeOnPICkitButtonToolStripMenuItem.Checked = false;

			KONST.MPLABerrorcodes errorcode = KONST.MPLABerrorcodes.Unknownerror;
			revertToMPLABModeToolStripMenuItem.Enabled = false;
			// TODO more error checking
			bool s = Pk2.SwitchToBootLoader(ref errorcode);

			if (s)
			{
				disableGUIControls();

				Thread.Sleep(2000); // Delay a bit so the PICkit 3 restarts

				KONST.PICkit2USB res = KONST.PICkit2USB.notFound;

				while (res == KONST.PICkit2USB.notFound)
				{
					res = Pk2.DetectPICkit2Device(pk2number, true);
					Thread.Sleep(500); // Delay a bit so we don't hammer the programmer
				}

				if (res == KONST.PICkit2USB.bootloader)
				{
					MessageBox.Show("The PICkit3 has been converted to MPLAB mode.\n\nExiting GUI.");
				}
				else
					MessageBox.Show("An error has occured. Try again.\n\nExiting GUI.");

				this.Close();
			}
			else
			{
				displayStatusWindow.Text = "No valid bootloader has been found at 0xF000\nCheck the Readme for more information.";
				displayStatusWindow.BackColor = Color.Salmon;
			}
		}

		private void showDownloadError(string s)
		{
			displayStatusWindow.Text = s;
			displayStatusWindow.BackColor = Color.Salmon;
			downloadPICkit2FirmwareToolStripMenuItem.Enabled = false;
			chkBoxVddOn.Enabled = false;
			numUpDnVDD.Enabled = false;
			deviceToolStripMenuItem.Enabled = false;
			disableGUIControls();
			return;
		}

		private void launchUsersGuidePK3(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + KONST.UserGuideFileNamePK3);
			}
			catch
			{
				MessageBox.Show("Unable to open User's Guide.");
			}
		}

		private void pickit3OnTheWeb(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start("http://www.microchip.com/pickit3");
			}
			catch
			{
				MessageBox.Show("Unable to open link.");
			}
		}

		private void uG44pinToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + "\\44-Pin Demo Board User Guide 41296B.pdf");
			}
			catch
			{
				MessageBox.Show("Unable to open 44-Pin Demo Board User Guide.");
			}
		}

		private void lpcUsersGuideToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(HomeDirectory + "\\Low Pin Count User Guide 51556a.pdf");
			}
			catch
			{
				MessageBox.Show("Unable to open Low Pin Count Demo Board User Guide.");
			}
		}
	}
}


