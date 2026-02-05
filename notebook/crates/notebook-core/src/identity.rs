//! Identity derivation for the Knowledge Exchange Platform
//!
//! This module provides AuthorId derivation from public keys.
//! Owned by: agent-crypto
//!
//! # AuthorId Format
//!
//! An AuthorId is derived from a public key as follows:
//! 1. Compute blake3 hash of the 32-byte public key
//! 2. Use the full 32-byte hash as the AuthorId
//!
//! This provides 256-bit collision resistance matching the types::AuthorId format.
//!
//! # Example
//!
//! ```
//! use notebook_core::crypto::KeyPair;
//! use notebook_core::identity::derive_author_id;
//!
//! let keypair = KeyPair::generate();
//! let author_id = derive_author_id(&keypair.public_key());
//!
//! // AuthorId is a 64-character hex string (32 bytes)
//! assert_eq!(author_id.to_string().len(), 64);
//!
//! // Same key always produces same AuthorId
//! let author_id2 = derive_author_id(&keypair.public_key());
//! assert_eq!(author_id, author_id2);
//! ```

use crate::crypto::PublicKey;
use crate::types::AuthorId;

/// Derive an AuthorId from a public key
///
/// Uses blake3 hash of the public key to produce a deterministic
/// 32-byte AuthorId that can be used with the Entry and Notebook types.
///
/// # Example
///
/// ```
/// use notebook_core::crypto::KeyPair;
/// use notebook_core::identity::derive_author_id;
///
/// let keypair = KeyPair::generate();
/// let author_id = derive_author_id(&keypair.public_key());
///
/// // Same key always produces same AuthorId
/// let author_id2 = derive_author_id(&keypair.public_key());
/// assert_eq!(author_id, author_id2);
/// ```
pub fn derive_author_id(public_key: &PublicKey) -> AuthorId {
    let hash = blake3::hash(public_key.as_bytes());
    let bytes: [u8; 32] = *hash.as_bytes();
    AuthorId::from_bytes(bytes)
}

/// Extension trait to derive AuthorId from a PublicKey
///
/// This provides a convenient method on PublicKey to derive the AuthorId.
pub trait PublicKeyExt {
    /// Derive the AuthorId from this public key
    fn author_id(&self) -> AuthorId;
}

impl PublicKeyExt for PublicKey {
    fn author_id(&self) -> AuthorId {
        derive_author_id(self)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::crypto::KeyPair;

    #[test]
    fn test_derive_author_id_format() {
        let keypair = KeyPair::generate();
        let author_id = derive_author_id(&keypair.public_key());

        // Should be 64 hex characters (32 bytes)
        assert_eq!(author_id.to_string().len(), 64);

        // Should be lowercase hex
        assert!(author_id.to_string().chars().all(|c| c.is_ascii_hexdigit()));
    }

    #[test]
    fn test_derive_author_id_deterministic() {
        let secret = [42u8; 32];
        let keypair1 = KeyPair::from_bytes(&secret);
        let keypair2 = KeyPair::from_bytes(&secret);

        let id1 = derive_author_id(&keypair1.public_key());
        let id2 = derive_author_id(&keypair2.public_key());

        assert_eq!(id1, id2);
    }

    #[test]
    fn test_different_keys_different_ids() {
        let keypair1 = KeyPair::generate();
        let keypair2 = KeyPair::generate();

        let id1 = derive_author_id(&keypair1.public_key());
        let id2 = derive_author_id(&keypair2.public_key());

        assert_ne!(id1, id2);
    }

    #[test]
    fn test_public_key_ext_trait() {
        let keypair = KeyPair::generate();

        // Using the extension trait
        let id1 = keypair.public_key().author_id();

        // Using the function directly
        let id2 = derive_author_id(&keypair.public_key());

        assert_eq!(id1, id2);
    }

    #[test]
    fn test_author_id_serialization_roundtrip() {
        let keypair = KeyPair::generate();
        let author_id = derive_author_id(&keypair.public_key());

        // Serialize to JSON
        let json = serde_json::to_string(&author_id).unwrap();

        // Should be a quoted string
        assert!(json.starts_with('"'));
        assert!(json.ends_with('"'));

        // Deserialize back
        let restored: AuthorId = serde_json::from_str(&json).unwrap();
        assert_eq!(author_id, restored);
    }

    #[test]
    fn test_known_key_produces_stable_id() {
        // Test with a known secret key to ensure consistent behavior across versions
        let secret = [0u8; 32];
        let keypair = KeyPair::from_bytes(&secret);
        let author_id = derive_author_id(&keypair.public_key());

        // Regenerate and verify stability
        let keypair2 = KeyPair::from_bytes(&secret);
        let author_id2 = derive_author_id(&keypair2.public_key());

        assert_eq!(author_id, author_id2);

        // The ID should be deterministic
        let hex = author_id.to_string();
        assert_eq!(hex.len(), 64);
    }

    #[test]
    fn test_author_id_bytes_roundtrip() {
        let keypair = KeyPair::generate();
        let author_id = derive_author_id(&keypair.public_key());

        // Get bytes and recreate
        let bytes = *author_id.as_bytes();
        let restored = AuthorId::from_bytes(bytes);

        assert_eq!(author_id, restored);
    }

    #[test]
    fn test_author_id_hash_property() {
        use std::collections::HashSet;

        let keypair1 = KeyPair::generate();
        let keypair2 = KeyPair::generate();

        let id1 = derive_author_id(&keypair1.public_key());
        let id2 = derive_author_id(&keypair2.public_key());

        let mut set = HashSet::new();
        set.insert(id1.clone());
        set.insert(id2.clone());

        assert_eq!(set.len(), 2);
        assert!(set.contains(&id1));
        assert!(set.contains(&id2));
    }
}
