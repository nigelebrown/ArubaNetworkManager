# Import the verified JCF Network Manager 0.9.0 source

The one-click Docker installer intentionally refuses to use the older 0.8.2 application.

## One-time publication of the full 0.9.0 source

1. Download the reviewed public-source asset from the ChatGPT delivery: `JCF_ArubaNetworkManager_v0_9_0_Public_Release.zip`.
2. Review the archive. Only public application source, tests, sanitized fixtures and deployment code should be published. It contains no runtime secrets by design.
3. Open **GitHub → ArubaNetworkManager → Releases → Draft a new release**.
4. Create the tag **`v0.9.0-source`** against `main`. Attach the ZIP with the **exact filename** above.
5. Publish the release. This triggers **Import Verified JCF v0.9.0 Source From Release** under GitHub Actions; it verifies the fixed SHA-256 and imports 100+ application files. If GitHub Actions is disabled or the event does not run, open Actions and run that workflow manually.
6. Check `src/Jcf.NetworkManager/Jcf.NetworkManager.csproj` on `main`: the version must be `0.9.0`, and `20261106123050_DeviceIntegration.cs` must exist.

Approved ZIP SHA-256:
```
7a33c77736ae8d13f8bdef4b95d34c37bd4e74c36e0f9e1a706ce938aa94d2e9
```

## After GitHub Actions completes

On CentOS Stream 9:

```bash
curl -fsSLo /tmp/JCF_INSTALL_LATEST.sh \
  https://raw.githubusercontent.com/nigelebrown/ArubaNetworkManager/main/INSTALL_LATEST_CENTOS9.sh
sudo bash /tmp/JCF_INSTALL_LATEST.sh
```

The new launcher checks the real 0.9.0 source and device-integration migration before invoking Docker Compose. The application initially runs in isolated demo mode. The AI reviewer is offline and restricted to redacted code; it has no route to the management network and no device credentials.

**Status note:** Committing the import workflow is not equivalent to importing the source. Do not run the Docker installation while the repository still displays 0.8.2.
