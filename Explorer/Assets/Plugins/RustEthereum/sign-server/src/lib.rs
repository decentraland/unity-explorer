pub mod cabi;
pub mod server;

use lazy_static::lazy_static;

lazy_static! {
    pub static ref SIGN_SERVER: server::SignServer = server::SignServer::default();
}
