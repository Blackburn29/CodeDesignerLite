// set memory location
address $000A0000
// jump or branch to label
jal :label
nop
beq v0, zero, :label:
nop
// print text
print "bla bla bla"
// import files
import "imports/test.cds"
// raw data
hexcode $64
hexcode :label
// set register to value or label
setreg t0, $3F800000
setreg t0, :label
// set label
label:
// float commands convert decimal values to float values
float $-1
float $1.00
float $10.5
// easy branch, b is treated as a standard branch like: beq zero, zero, :label
b :label
nop
// labels starting with "FNC" are bold indicating the start of a function
FNC_Label:
fncLabel:
// multiple formats are supported
addiu $s0, $v0, 0x10
addiu s0, v0, 0x10
addiu s0, v0, $10
// float registers are highlighted unless using $ format
mtc1 f0, t0
mtc1 $f0, t0


//-------------ERROR HANDLING
// Errors will be highlighted and displayed below when compiled
boop
