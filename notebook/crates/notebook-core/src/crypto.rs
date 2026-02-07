//! Cryptographic primitives for the Knowledge Exchange Platform
//!
//! This module provides Ed25519 signing and verification for entries.
//! Owned by: agent-crypto
//!
//! # Example
//!
//! ```
//! use notebook_core::crypto::{KeyPair, SignableContent};
//!
//! let keypair = KeyPair::generate();
//! let content = SignableContent {
//!     content: b"Hello, world!".to_vec(),
//!     content_type: "text/plain".to_string(),
//!     topic: Some("greeting".to_string()),
//!     references: vec![],
//!     revision_of: None,
//! };
//!
//! let signature = keypair.sign(&content);
//! assert!(keypair.public_key().verify(&content, &signature).is_ok());
//! ```

use base64::{Engine as _, engine::general_purpose::STANDARD as BASE64};
use ed25519_dalek::{Signature as DalekSignature, Signer, SigningKey, Verifier, VerifyingKey};
use rand::rngs::OsRng;
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use std::fmt;

/// Error type for cryptographic operations
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CryptoError {
    /// Invalid key bytes (wrong length or malformed)
    InvalidKey(String),
    /// Invalid signature bytes (wrong length or malformed)
    InvalidSignature(String),
    /// Signature verification failed
    VerificationFailed,
    /// Serialization error
    SerializationError(String),
}

impl fmt::Display for CryptoError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            CryptoError::InvalidKey(msg) => write!(f, "invalid key: {}", msg),
            CryptoError::InvalidSignature(msg) => write!(f, "invalid signature: {}", msg),
            CryptoError::VerificationFailed => write!(f, "signature verification failed"),
            CryptoError::SerializationError(msg) => write!(f, "serialization error: {}", msg),
        }
    }
}

impl std::error::Error for CryptoError {}

/// Ed25519 public key (32 bytes)
///
/// Serializes to/from base64 for JSON compatibility.
#[derive(Clone, PartialEq, Eq)]
pub struct PublicKey(VerifyingKey);

impl PublicKey {
    /// Create a PublicKey from raw bytes
    pub fn from_bytes(bytes: &[u8; 32]) -> Result<Self, CryptoError> {
        VerifyingKey::from_bytes(bytes)
            .map(PublicKey)
            .map_err(|e| CryptoError::InvalidKey(e.to_string()))
    }

    /// Get the raw bytes of the public key
    pub fn as_bytes(&self) -> &[u8; 32] {
        self.0.as_bytes()
    }

    /// Verify a signature over content
    pub fn verify(
        &self,
        content: &SignableContent,
        signature: &Signature,
    ) -> Result<(), CryptoError> {
        let payload = content
            .canonical_bytes()
            .map_err(|e| CryptoError::SerializationError(e.to_string()))?;

        self.0
            .verify(&payload, &signature.0)
            .map_err(|_| CryptoError::VerificationFailed)
    }
}

impl fmt::Debug for PublicKey {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "PublicKey({})", BASE64.encode(self.as_bytes()))
    }
}

impl Serialize for PublicKey {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let encoded = BASE64.encode(self.as_bytes());
        serializer.serialize_str(&encoded)
    }
}

impl<'de> Deserialize<'de> for PublicKey {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let encoded = String::deserialize(deserializer)?;
        let bytes = BASE64.decode(&encoded).map_err(serde::de::Error::custom)?;

        if bytes.len() != 32 {
            return Err(serde::de::Error::custom(format!(
                "public key must be 32 bytes, got {}",
                bytes.len()
            )));
        }

        let mut arr = [0u8; 32];
        arr.copy_from_slice(&bytes);
        PublicKey::from_bytes(&arr).map_err(serde::de::Error::custom)
    }
}

/// Ed25519 signature (64 bytes)
///
/// Serializes to/from base64 for JSON compatibility.
#[derive(Clone, PartialEq, Eq)]
pub struct Signature(DalekSignature);

impl Signature {
    /// Create a Signature from raw bytes
    pub fn from_bytes(bytes: &[u8; 64]) -> Result<Self, CryptoError> {
        Ok(Signature(DalekSignature::from_bytes(bytes)))
    }

    /// Get the raw bytes of the signature
    pub fn to_bytes(&self) -> [u8; 64] {
        self.0.to_bytes()
    }
}

impl fmt::Debug for Signature {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let bytes = self.to_bytes();
        write!(f, "Signature({}...)", &BASE64.encode(&bytes[..8]))
    }
}

impl Serialize for Signature {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let encoded = BASE64.encode(self.to_bytes());
        serializer.serialize_str(&encoded)
    }
}

impl<'de> Deserialize<'de> for Signature {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        let encoded = String::deserialize(deserializer)?;
        let bytes = BASE64.decode(&encoded).map_err(serde::de::Error::custom)?;

        if bytes.len() != 64 {
            return Err(serde::de::Error::custom(format!(
                "signature must be 64 bytes, got {}",
                bytes.len()
            )));
        }

        let mut arr = [0u8; 64];
        arr.copy_from_slice(&bytes);
        Signature::from_bytes(&arr).map_err(serde::de::Error::custom)
    }
}

/// Ed25519 keypair for signing entries
///
/// The keypair contains both the secret signing key and the public verifying key.
/// Keep the keypair secure - anyone with access can sign entries as this identity.
pub struct KeyPair {
    signing_key: SigningKey,
}

impl KeyPair {
    /// Generate a new random keypair
    pub fn generate() -> Self {
        let signing_key = SigningKey::generate(&mut OsRng);
        KeyPair { signing_key }
    }

    /// Create a keypair from a 32-byte secret key
    pub fn from_bytes(secret_key: &[u8; 32]) -> Self {
        let signing_key = SigningKey::from_bytes(secret_key);
        KeyPair { signing_key }
    }

    /// Get the public key for this keypair
    pub fn public_key(&self) -> PublicKey {
        PublicKey(self.signing_key.verifying_key())
    }

    /// Get the secret key bytes (for secure storage)
    pub fn secret_key_bytes(&self) -> &[u8; 32] {
        self.signing_key.as_bytes()
    }

    /// Sign content with this keypair
    pub fn sign(&self, content: &SignableContent) -> Signature {
        let payload = content
            .canonical_bytes()
            .expect("serialization should not fail for valid content");
        Signature(self.signing_key.sign(&payload))
    }
}

impl fmt::Debug for KeyPair {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "KeyPair(public_key: {:?})", self.public_key())
    }
}

/// Content to be signed
///
/// This struct contains all the fields that are included in the signature.
/// The signature covers the canonical JSON serialization of these fields.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SignableContent {
    /// The raw content bytes
    pub content: Vec<u8>,
    /// MIME type of the content
    pub content_type: String,
    /// Optional topic for categorization
    pub topic: Option<String>,
    /// References to other entries (by ID)
    pub references: Vec<String>,
    /// If this is a revision, the ID of the entry being revised
    pub revision_of: Option<String>,
}

impl SignableContent {
    /// Serialize to canonical bytes for signing
    ///
    /// Uses serde_json for deterministic serialization.
    pub fn canonical_bytes(&self) -> Result<Vec<u8>, serde_json::Error> {
        serde_json::to_vec(self)
    }
}

/// Sign entry content with a keypair
///
/// This is a convenience function that creates a SignableContent and signs it.
pub fn sign_entry(
    content: &[u8],
    content_type: &str,
    topic: Option<&str>,
    references: &[String],
    revision_of: Option<&str>,
    keypair: &KeyPair,
) -> Signature {
    let signable = SignableContent {
        content: content.to_vec(),
        content_type: content_type.to_string(),
        topic: topic.map(String::from),
        references: references.to_vec(),
        revision_of: revision_of.map(String::from),
    };
    keypair.sign(&signable)
}

/// Verify an entry signature
///
/// This is a convenience function that creates a SignableContent and verifies it.
pub fn verify_entry(
    content: &[u8],
    content_type: &str,
    topic: Option<&str>,
    references: &[String],
    revision_of: Option<&str>,
    signature: &Signature,
    public_key: &PublicKey,
) -> Result<(), CryptoError> {
    let signable = SignableContent {
        content: content.to_vec(),
        content_type: content_type.to_string(),
        topic: topic.map(String::from),
        references: references.to_vec(),
        revision_of: revision_of.map(String::from),
    };
    public_key.verify(&signable, signature)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_keypair_generation() {
        let keypair = KeyPair::generate();
        let public_key = keypair.public_key();

        // Public key should be 32 bytes
        assert_eq!(public_key.as_bytes().len(), 32);

        // Secret key should be 32 bytes
        assert_eq!(keypair.secret_key_bytes().len(), 32);
    }

    #[test]
    fn test_keypair_from_bytes_deterministic() {
        let secret = [42u8; 32];
        let keypair1 = KeyPair::from_bytes(&secret);
        let keypair2 = KeyPair::from_bytes(&secret);

        assert_eq!(
            keypair1.public_key().as_bytes(),
            keypair2.public_key().as_bytes()
        );
    }

    #[test]
    fn test_sign_verify_roundtrip() {
        let keypair = KeyPair::generate();
        let content = SignableContent {
            content: b"Hello, Knowledge Exchange!".to_vec(),
            content_type: "text/plain".to_string(),
            topic: Some("greeting".to_string()),
            references: vec!["entry-123".to_string()],
            revision_of: None,
        };

        let signature = keypair.sign(&content);
        let result = keypair.public_key().verify(&content, &signature);

        assert!(result.is_ok());
    }

    #[test]
    fn test_tampered_content_fails_verification() {
        let keypair = KeyPair::generate();
        let original = SignableContent {
            content: b"Original content".to_vec(),
            content_type: "text/plain".to_string(),
            topic: None,
            references: vec![],
            revision_of: None,
        };

        let signature = keypair.sign(&original);

        // Tamper with the content
        let tampered = SignableContent {
            content: b"Tampered content".to_vec(),
            ..original
        };

        let result = keypair.public_key().verify(&tampered, &signature);
        assert_eq!(result, Err(CryptoError::VerificationFailed));
    }

    #[test]
    fn test_wrong_key_fails_verification() {
        let keypair1 = KeyPair::generate();
        let keypair2 = KeyPair::generate();

        let content = SignableContent {
            content: b"Test content".to_vec(),
            content_type: "text/plain".to_string(),
            topic: None,
            references: vec![],
            revision_of: None,
        };

        let signature = keypair1.sign(&content);

        // Verify with wrong key should fail
        let result = keypair2.public_key().verify(&content, &signature);
        assert_eq!(result, Err(CryptoError::VerificationFailed));
    }

    #[test]
    fn test_public_key_serialization() {
        let keypair = KeyPair::generate();
        let public_key = keypair.public_key();

        // Serialize to JSON
        let json = serde_json::to_string(&public_key).unwrap();

        // Should be a base64 string
        assert!(json.starts_with('"'));
        assert!(json.ends_with('"'));

        // Deserialize back
        let restored: PublicKey = serde_json::from_str(&json).unwrap();
        assert_eq!(public_key.as_bytes(), restored.as_bytes());
    }

    #[test]
    fn test_signature_serialization() {
        let keypair = KeyPair::generate();
        let content = SignableContent {
            content: b"Test".to_vec(),
            content_type: "text/plain".to_string(),
            topic: None,
            references: vec![],
            revision_of: None,
        };

        let signature = keypair.sign(&content);

        // Serialize to JSON
        let json = serde_json::to_string(&signature).unwrap();

        // Should be a base64 string
        assert!(json.starts_with('"'));
        assert!(json.ends_with('"'));

        // Deserialize back
        let restored: Signature = serde_json::from_str(&json).unwrap();
        assert_eq!(signature.to_bytes(), restored.to_bytes());

        // Restored signature should still verify
        let result = keypair.public_key().verify(&content, &restored);
        assert!(result.is_ok());
    }

    #[test]
    fn test_sign_entry_convenience_function() {
        let keypair = KeyPair::generate();

        let signature = sign_entry(
            b"Content",
            "text/plain",
            Some("test-topic"),
            &["ref-1".to_string()],
            None,
            &keypair,
        );

        let result = verify_entry(
            b"Content",
            "text/plain",
            Some("test-topic"),
            &["ref-1".to_string()],
            None,
            &signature,
            &keypair.public_key(),
        );

        assert!(result.is_ok());
    }

    #[test]
    fn test_all_fields_affect_signature() {
        let keypair = KeyPair::generate();
        let base = SignableContent {
            content: b"Content".to_vec(),
            content_type: "text/plain".to_string(),
            topic: Some("topic".to_string()),
            references: vec!["ref".to_string()],
            revision_of: Some("rev".to_string()),
        };

        let signature = keypair.sign(&base);

        // Change content
        let mut modified = base.clone();
        modified.content = b"Different".to_vec();
        assert!(keypair.public_key().verify(&modified, &signature).is_err());

        // Change content_type
        let mut modified = base.clone();
        modified.content_type = "application/json".to_string();
        assert!(keypair.public_key().verify(&modified, &signature).is_err());

        // Change topic
        let mut modified = base.clone();
        modified.topic = Some("other".to_string());
        assert!(keypair.public_key().verify(&modified, &signature).is_err());

        // Change references
        let mut modified = base.clone();
        modified.references = vec!["other-ref".to_string()];
        assert!(keypair.public_key().verify(&modified, &signature).is_err());

        // Change revision_of
        let mut modified = base.clone();
        modified.revision_of = Some("other-rev".to_string());
        assert!(keypair.public_key().verify(&modified, &signature).is_err());
    }
}
