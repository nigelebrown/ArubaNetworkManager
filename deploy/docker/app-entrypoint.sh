#!/bin/sh
set -eu
if [ "${Jcf__DemoMode:-true}" = "false" ]; then
  [ -r /run/secrets/sql_connection ] || { echo 'JCF blocked: SQL secret missing' >&2; exit 20; }
  export ConnectionStrings__NetworkManager="$(cat /run/secrets/sql_connection)"
  [ -n "$ConnectionStrings__NetworkManager" ] || { echo 'JCF blocked: empty SQL secret' >&2; exit 21; }
  if [ -r /run/secrets/bootstrap_admin_password ]; then
    export Jcf__BootstrapAdmin__PasswordFile=/run/secrets/bootstrap_admin_password
  fi
fi
exec dotnet /app/Jcf.NetworkManager.dll
