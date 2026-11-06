#!/usr/bin/env bash
# Allow http://172.17.71.20 solely as an HTTP -> HTTPS-FQDN redirect.
# Requires working real-Identity v0.9.1 over trusted HTTPS first.
set -Eeuo pipefail
umask 027
fail(){ printf '[JCF HTTPS Redirect] STOPPED: %s\n' "$*" >&2; exit 1; }
[[ ${EUID} -eq 0 ]] || fail 'Run as root'
source /etc/os-release
[[ "${ID:-}" == centos && "${VERSION_ID%%.*}" == 9 ]] || fail 'CentOS Stream 9 required'
[[ "$(systemctl is-active jcf-network-manager-v091)" == active ]] || fail 'Real Identity app v0.9.1 not running'
[[ "$(systemctl is-active nginx)" == active ]] || fail 'Nginx HTTPS is not running'
CONF=/etc/nginx/conf.d/jcf-network-manager-v091.conf
[[ -r "$CONF" ]] || fail 'Secure v0.9.1 reverse proxy is missing'
curl -fsS --max-time 5 http://127.0.0.1:5181/healthz | python3 -c 'import json,sys;v=json.load(sys.stdin);assert v["status"]=="up" and v["version"]=="0.9.1" and v["demo"] is False' || fail 'v0.9.1 production health check failed'
curl -fsS --max-time 8 -H 'X-Forwarded-Proto: https' http://127.0.0.1:5181/readyz >/dev/null || fail 'SQL backend is not ready'
IP=172.17.71.20
ip -o -4 addr show scope global | awk '{split($4,a,"/");print a[1]}' | grep -Fxq "$IP" || fail 'Configured server IP 172.17.71.20 not assigned'
[[ -z "$(ss -H -lnt '( sport = :80 )')" ]] || fail 'Port 80 already has a listener; refusing to replace it'
HOST="$(awk '/^[[:space:]]*server_name[[:space:]]/{gsub(";","",$2);print $2;exit}' "$CONF")"
[[ "$HOST" =~ ^[A-Za-z0-9][A-Za-z0-9.-]{2,250}$ && "$HOST" == *.* ]] || fail 'No valid HTTPS FQDN in existing configuration'
ALLOWS="$(sed -nE 's/^[[:space:]]*allow[[:space:]]+([0-9./]+);[[:space:]]*$/\1/p' "$CONF")"
[[ -n "$ALLOWS" ]] || fail 'Nginx HTTPS allowlist missing'
export ALLOWS
python3 - <<'PY' || fail 'HTTPS network allowlist invalid'
import ipaddress,os
nets=os.environ['ALLOWS'].splitlines()
assert 1<=len(nets)<=30
for s in nets:
    n=ipaddress.IPv4Network(s,strict=False)
    assert n.is_private and n.prefixlen>=16
PY
DEST=/etc/nginx/conf.d/jcf-http-ip-redirect.conf
[[ ! -e "$DEST" ]] || fail 'Redirect configuration exists already; inspect before replacement'
{
  printf 'server {\n    listen %s:80;\n    server_name _;\n    access_log off;\n    location / {\n' "$IP"
  while IFS= read -r net; do printf '        allow %s;\n' "$net"; done <<< "$ALLOWS"
  printf '        deny all;\n        return 308 https://%s$request_uri;\n    }\n}\n' "$HOST"
} > "$DEST"
chmod 0644 "$DEST"
if ! nginx -t; then rm -f "$DEST"; fail 'Nginx validation failed; config reverted'; fi
systemctl reload nginx
IFACE="$(ip -o -4 addr show scope global | awk -v ip="$IP" '{split($4,a,"/");if(a[1]==ip){print $2;exit}}')"
ZONE="$(firewall-cmd --get-zone-of-interface="$IFACE" 2>/dev/null || true)"
[[ -n "$ZONE" && "$ZONE" != 'no zone' ]] || fail 'Firewall zone unresolved'
while IFS= read -r net; do
  RULE="rule family=ipv4 source address=\"$net\" port port=\"80\" protocol=\"tcp\" accept"
  firewall-cmd --permanent --zone="$ZONE" --add-rich-rule="$RULE" >/dev/null
  firewall-cmd --zone="$ZONE" --add-rich-rule="$RULE" >/dev/null
done <<< "$ALLOWS"
printf '[JCF HTTPS Redirect] SUCCESS: http://%s now redirects to https://%s for approved clients only.\n' "$IP" "$HOST"
