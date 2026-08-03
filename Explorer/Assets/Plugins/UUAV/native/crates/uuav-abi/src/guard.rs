
#[macro_export]
macro_rules! assert_same_layout {
    (
        abi: $abi:ty,
        core: $core:ty,
        fields { $($field:ident: $field_ty:ty),+ $(,)? }
    ) => {
        const _: () = {
            assert!(::core::mem::size_of::<$abi>() == ::core::mem::size_of::<$core>());
            assert!(::core::mem::align_of::<$abi>() == ::core::mem::align_of::<$core>());
        };

        $(
            const _: () = {
                #[allow(dead_code)]
                const fn pin_field(value: $core) -> $field_ty {
                    value.$field
                }
                assert!(
                    ::core::mem::offset_of!($abi, $field)
                        == ::core::mem::offset_of!($core, $field)
                );
            };
        )+

        const _: () = {
            type Core = $core;
            #[allow(dead_code)]
            const fn exhaustive(value: Core) {
                let Core { $($field: _),+ } = value;
            }
        };
    };
}

#[macro_export]
macro_rules! assert_enum_discriminants {
    (
        abi: $abi:ty,
        core: $core:ty,
        variants { $($variant:ident),+ $(,)? }
    ) => {
        const _: () = {
            type Abi = $abi;
            type Core = $core;

            #[allow(dead_code)]
            const fn exhaustive(value: Core) -> i64 {
                match value {
                    $(Core::$variant => Abi::$variant as i64),+
                }
            }

            assert!(::core::mem::size_of::<Abi>() == ::core::mem::size_of::<Core>());
            assert!(::core::mem::align_of::<Abi>() == ::core::mem::align_of::<Core>());
            $(assert!(Abi::$variant as i64 == Core::$variant as i64);)+
        };
    };
}
