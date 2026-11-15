# BODEX VPN tunnel integration — engineering notes

## Status

This document records the implementation boundary for the experimental
`feature/wintun-vpn-tunnel` branch. It is not a claim that a VPN tunnel has
been implemented or tested.

## Existing BODEX engine

The current Windows build clones ProxiFyre v2.7.0 during CI and applies the
BODEX branding and shared-license patches. ProxiFyre is based on the Windows
Packet Filter / NDISRD interception engine. Its process-routing model is not a
Wintun interface and does not, by itself, provide a strict device-wide VPN
boundary.

The existing shared activation mechanism is outside the tunnel-engine change.
Do not change the Cloudflare license API, activation format, or shared code
used by HB Browser and BODEX Proxy.

## Candidate TUN engine

The evaluated candidate is `xjasonlyu/tun2socks`. Its current source has a
Windows TUN implementation backed by WireGuard's Wintun integration. The
engine accepts a SOCKS5 proxy URL (including username/password), a TUN device
name, and an optional MTU. Its SOCKS5 transport implements TCP CONNECT and UDP
ASSOCIATE. The upstream project currently targets a recent Go toolchain; pin a
specific reviewed release or commit before adding it to a reproducible build.

The upstream project is MIT-licensed. Keep its copyright and license notice
with any redistributed binary/source. ProxiFyre remains AGPL-3.0; distribution
of the combined BODEX product must continue to satisfy all applicable upstream
license obligations.

## Required BODEX integration

The TUN engine must be supervised by a privileged Windows service, not started
as an unmanaged child process from the UI. The UI should communicate with the
service through an authenticated local IPC boundary and display only service-
reported state.

Startup ordering is security-critical:

1. Validate the existing BODEX license without changing the shared license
   contract.
2. Resolve and validate the SOCKS5 endpoint and credentials.
3. Install a fail-closed Windows Filtering Platform (WFP) policy before
   changing default routes. Permit only the physical-interface traffic needed
   to reach the configured SOCKS5 endpoint and essential local control traffic.
4. Create/open the Wintun interface and start the TUN-to-SOCKS5 engine.
5. Add explicit IPv4 and IPv6 routes through the TUN interface, while retaining
   a host route for the SOCKS5 endpoint through the physical gateway to prevent
   recursive tunnelling.
6. Configure DNS so conventional DNS cannot bypass the tunnel. Define and test
   behavior for DoH, DoT, DoQ, multicast DNS, and cached resolver results.
7. Verify that the TUN engine is forwarding traffic before reporting Connected.

Shutdown and failure handling:

- On user stop, service crash, upstream SOCKS5 loss, or TUN engine exit, keep
  the WFP block policy active until default routes and DNS are safely restored.
- On unexpected failure, fail closed; never fall back to direct internet.
- Restore only the routes, DNS settings, and firewall state owned by BODEX.
- Make cleanup idempotent and recoverable after process or machine restart.

## Acceptance tests before release

- IPv4 public address is the configured proxy exit address.
- IPv6 either traverses the proxy correctly or is blocked; it must not escape
  directly.
- DNS leak test confirms resolvers are reached only through the tunnel.
- TCP and UDP tests pass through the SOCKS5 server, including authentication.
- Disconnecting the SOCKS5 server does not permit direct traffic.
- Killing the service or TUN process leaves the machine fail-closed until
  recovery/cleanup runs.
- Stopping BODEX restores the original routes, DNS configuration, and ordinary
  internet connectivity.
- Reboot/recovery tests confirm no persistent routing or firewall damage.
- Existing activation, shared license code, and HB Browser compatibility tests
  pass unchanged.

## Important limitation

A Wintun adapter plus a running tun2socks process is not sufficient evidence
of a secure VPN. Route management, SOCKS5 endpoint bypass, DNS policy, WFP
kill-switch lifecycle, crash recovery, and real leak tests are mandatory.
