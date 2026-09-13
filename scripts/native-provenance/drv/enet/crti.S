/* crti.S — byte-equivalent to glibc's sysdeps/x86_64/crti.S as built on
   Ubuntu 22.04 (glibc --enable-cet => _CET_ENDBR = endbr64).
   Carries the GNU property note (IBT|SHSTK) so ld's AND keeps it. */
	.section .note.gnu.property,"a"
	.p2align 3
	.long 1f - 0f
	.long 3f - 2f
	.long 5
0:	.asciz "GNU"
1:	.p2align 3
2:	.long 0xc0000002
	.long 4
	.long 0x3
	.p2align 3
3:

	.section .init,"ax",@progbits
	.p2align 2
	.globl _init
	.hidden _init
	.type _init, @function
_init:
	endbr64
	subq $8, %rsp
	movq __gmon_start__@GOTPCREL(%rip), %rax
	testq %rax, %rax
	je 1f
	call *%rax
1:

	.section .fini,"ax",@progbits
	.p2align 2
	.globl _fini
	.hidden _fini
	.type _fini, @function
_fini:
	endbr64
	subq $8, %rsp

	.weak __gmon_start__

	.section .note.GNU-stack,"",@progbits
