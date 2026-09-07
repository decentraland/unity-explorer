/* crtn.S — glibc sysdeps/x86_64/crtn.S epilogues, with GNU property note. */
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
	addq $8, %rsp
	ret

	.section .fini,"ax",@progbits
	addq $8, %rsp
	ret

	.section .note.GNU-stack,"",@progbits
