;******************************************************************************
;Software License Agreement                                         
;                                                                    
;The software supplied herewith by Microchip Technology             
;Incorporated (the "Company") is intended and supplied to you, the  
;Company’s customer, for use solely and exclusively on Microchip    
;products. The software is owned by the Company and/or its supplier,
;and is protected under applicable copyright laws. All rights are   
;reserved. Any use in violation of the foregoing restrictions may   
;subject the user to criminal sanctions under applicable laws, as   
;well as to civil liability for the breach of the terms and         
;conditions of this license.                                        
;                                                                    
;THIS SOFTWARE IS PROVIDED IN AN "AS IS" CONDITION. NO WARRANTIES,  
;WHETHER EXPRESS, IMPLIED OR STATUTORY, INCLUDING, BUT NOT LIMITED  
;TO, IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A       
;PARTICULAR PURPOSE APPLY TO THIS SOFTWARE. THE COMPANY SHALL NOT,  
;IN ANY CIRCUMSTANCES, BE LIABLE FOR SPECIAL, INCIDENTAL OR         
;CONSEQUENTIAL DAMAGES, FOR ANY REASON WHATSOEVER.       
; *******************************************************************
; PICkit 2 Debug Demo - Reversible LEDs
;
; This demo reads the potentiometer voltage coming into RA0
; on the 44-pin Demo Board with the A2C converter.  The A2D
; high order bits are used to set a delay affecting the rate
; of the LED rotation.  The Demo Board switch is used to 
; reverse the direction of rotation of the LEDs.
;
; An intentional error exists in this demo code to prevent
; the A2D value from affecting the LED rate.
; See the PICkit 2 User's Guide (DS51553) for a tutorial
; on debugging with PICkit 2 using this demo code.
;


#include <p16f887.inc>
	__CONFIG    _CONFIG1, _LVP_OFF & _FCMEN_OFF & _IESO_OFF & _BOR_OFF & _CPD_OFF & _CP_OFF & _MCLRE_ON & _PWRTE_ON & _WDT_OFF & _INTRC_OSC_NOCLKOUT
	__CONFIG    _CONFIG2, _WRT_OFF & _BOR21V

     cblock     0x20
Delay : 2      ; Assign two addresses to label Delay
Display        ; define a variable to hold the diplay
Direction 
LookingFor     
     endc

     org       0x0000
STARTUP:
     pagesel   Main
     goto      Main

	org       0x0004             ; interrupt vector location
     ; (not used)

Main:
SetupPinDirections:
     banksel   TRISA          ; select Register Page 1
     movlw     0xFF
     movwf     TRISA          ; Make PortA all input
     clrf      TRISD          ; Make PortD all output
SetupAnalogPins:
     banksel   ANSEL          ; address Register Page 2
     bsf       STATUS,RP1     
     movlw     0x01           ; configure Port A pin 0 Analog
     movwf     ANSEL
     movlw     0x00           ; remaining pins are all digital
     movwf     ANSELH         
SetupA2D:
     banksel   ADCON1         ; address Register Page 1
     movlw     0x00           ; A2D Left-Justified, references are VDD and VSS
     movwf     ADCON1
     banksel   ADCON0         ; address Register Page 0
     movlw     0x41
     movwf     ADCON0         ; configure A2D for Fosc/8, Channel 0 (RA0), and turn on the A2D module
InitRegisters:
     banksel   Display        
     movlw     0x80
     movwf     Display
     clrf      Direction
     clrf      LookingFor     ; Looking for a 0 on the button
MainLoop:
     movf      Display,w      ; Copy the display to the LEDs
     movwf     PORTD
     nop                      ; wait total of 5uS for A2D amp to settle and capacitor to charge.
     nop                      ; wait 1uS
     nop                      ; wait 1uS
     nop                      ; wait 1uS
     nop                      ; wait 1uS
     bsf       ADCON0,GO_DONE ; start conversion
     btfsc     ADCON0,GO_DONE ; this bit will change to zero when the conversion is complete
     goto      $-1
     movf      ADRESH,w       ; Copy the A2D result to the delay loop
     movwf     Delay+1

A2DDelayLoop:
     incfsz    Delay,f        ; Waste time.  
     goto      A2DDelayLoop   ; The Inner loop takes 3 instructions per loop * 256 loopss = 768 instructions
     incfsz    Delay+1,f      ; The outer loop takes and additional 3 instructions per lap * 256 loops
     goto      A2DDelayLoop   ; (768+3) * 256 = 197376 instructions / 1M instructions per second = 0.197 sec.
                              ; call it a two-tenths of a second.

     movlw     .13            ; Delay another 10mS plus whatever was above
     movwf     Delay+1
TenmSdelay:     
     decfsz    Delay,f
     goto      TenmSdelay
     decfsz    Delay+1,f
     goto      TenmSdelay

     btfsc     LookingFor,0
     goto      LookingFor1
LookingFor0:
     btfsc     PORTB,0        ; is the switch pressed (0)
     goto      Rotate
     bsf       LookingFor,0   ; yes  Next we'll be looking for a 1
     movlw     0xFF           ; load the W register incase we need it
     xorwf     Direction,f    ; yes, flip the direction bit
     goto      Rotate

LookingFor1:
     btfsc     PORTB,0        ; is the switch pressed (0)
     bcf       LookingFor,0
     
Rotate:
     bcf       STATUS,C       ; ensure the carry bit is clear
     btfss     Direction,0
     goto      RotateLeft
RotateRight:
     rrf       Display,f
     btfsc     STATUS,C       ; Did the bit rotate into the carry?
     bsf       Display,7      ; yes, put it into bit 3.

     goto      MainLoop
RotateLeft:
     rlf       Display,f
     btfsc     STATUS,C       ; did it rotate out of the display
     bsf       Display,0      ; yes, put it into bit 0
     goto      MainLoop
     
     end
     
