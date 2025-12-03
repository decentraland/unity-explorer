use std::sync::Mutex;

use alloy_primitives::B256;
use alloy_signer::{k256::ecdsa::SigningKey, SignerSync};
use alloy_signer_local::{LocalSigner, PrivateKeySigner};

pub struct SignServer {
    signer: Mutex<Option<LocalSigner<SigningKey>>>,
}

impl Default for SignServer {
    fn default() -> Self {
        Self {
            signer: Default::default(),
        }
    }
}

impl SignServer {
    pub fn setup(&self, private_key: &[u8]) -> Result<(), std::fmt::Error> {
        let fixed_bytes = B256::from_slice(private_key);
        let create_result = PrivateKeySigner::from_bytes(&fixed_bytes);
        if create_result.is_err() {
            return Err(std::fmt::Error);
        }

        let signer = create_result.unwrap();

        match self.signer.lock() {
            Ok(mut guard) => {
                // If lock is successful, set the signer
                *guard = Some(signer);
                Ok(())
            }
            Err(_e) => Err(std::fmt::Error::default()),
        }
    }

    pub fn sign_message(&self, message: &str) -> Result<[u8; 65], std::fmt::Error> {
        match self.signer.lock() {
            Ok(guard) => {
                let signer = guard.as_ref().unwrap();
                let bytes = message.as_bytes();
                let sign_result = signer.sign_message_sync(bytes);
                match sign_result {
                    Ok(signature) => {
                        return Ok(signature.as_bytes());
                    }
                    Err(_e) => {
                        return Err(std::fmt::Error::default());
                    }
                }
            }
            Err(_e) => Err(std::fmt::Error::default()),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sign() {
        //0x64fdd126fe0e2de2ccbea065d710e9939d083ec96bb9933b750013f30ee81004
        let key_str = "64fdd126fe0e2de2ccbea065d710e9939d083ec96bb9933b750013f30ee81004";
        let message = "Test message";
        //0x578d0780163581456421895b03b79e038ab898d013450b3b58e20432fd89a0b54b86ae68348972fb8ca7544a88327e8628d32ba3e3a703b0e988f348ba26c3da1b
        let required_signature = "578d0780163581456421895b03b79e038ab898d013450b3b58e20432fd89a0b54b86ae68348972fb8ca7544a88327e8628d32ba3e3a703b0e988f348ba26c3da1b";

        let server = SignServer::default();
        let vec_key = hex::decode(key_str).unwrap();
        let private_key = vec_key.as_slice();
        server.setup(private_key).unwrap();

        let signature = server.sign_message(message).unwrap();

        let signature_str = hex::encode(signature);

        assert_eq!(signature_str, required_signature);
    }
}
