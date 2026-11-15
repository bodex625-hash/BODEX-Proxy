# Wintun engine build verification

## Verified artifact

The GitHub Actions workflow `.github/workflows/build-wintun-engine.yml` completed successfully on Windows Server 2022 for the pinned upstream tag `xjasonlyu/tun2socks v2.7.0`.

- Workflow run: https://github.com/bodex625-hash/BODEX-Proxy/actions/runs/43681294069
- Artifact: `BODEX-Wintun-Engine-v2.7.0-windows-x64`
- Target: Windows amd64
- Go toolchain: Go 1.26.3 (matches the `go 1.26.3` directive in the pinned tag's `go.mod`)
- Build: successful
- CLI version invocation: successful
- SHA-256: `f88bbca50d08fed0ed4da91d95f5001dd6d351eae5f913cd66d5b2081c9a7c5b`

The artifact contains:
- `BODEX-Tun2Socks.exe`
- `BODEX-Tun2Socks-build-info.txt`
- `SHA256SUMS.txt`
- `tun2socks-MIT-LICENSE.txt`

## Confirmed Windows device syntax

The pinned v2.7.0 source registers the Windows TUN parser and calls WireGuard's Wintun `CreateTUN` implementation. Its CLI accepts a device in `[driver://]name` form. The Windows TUN driver is `tun`, so the device argument is `tun://BODEX-TUN` (a stable, space-free adapter name).

The engine requires both a proxy and a device. Example syntax only:

```powershell
.\BODEX-Tun2Socks.exe --proxy "socks5://USER:PASSWORD@HOST:PORT" --device "tun://BODEX-TUN" --mtu 1500 --loglevel info
```

Credentials containing reserved URI characters must be percent-encoded. Replace the example credentials and endpoint with the user's configured SOCKS5 values. Do not put real credentials in committed files, command logs, or CI artifacts.

## Important integration boundary

This artifact proves that the pinned Wintun/tun2socks engine can be built for Windows. It does **not** configure Windows interface addresses, routes, DNS, firewall policy, a kill switch, a Windows service, or crash recovery. The tun2socks engine's `--tun-pre-up` and `--tun-post-up` hooks are not a substitute for a transactional Windows network controller; hook failures are logged by the upstream engine rather than providing the complete rollback and fail-closed contract required by BODEX.

Do not ship this engine as a working system-wide VPN until the BODEX service/controller is implemented and verified against the acceptance tests in `VPN-TUNNEL-INTEGRATION.md`.
