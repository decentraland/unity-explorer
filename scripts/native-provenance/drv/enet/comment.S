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

	.section .comment,"MS",@progbits,1
	.asciz "GCC: (Ubuntu 11.4.0-1ubuntu1~22.04) 11.4.0"
	.section .note.GNU-stack,"",@progbits
