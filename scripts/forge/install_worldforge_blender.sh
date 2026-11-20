#!/bin/bash
# Install the pinned, upstream-verified LTS bundle and the private World Forge worker on the Ubuntu x64 VPS.
set -euo pipefail
test "$(id -u)" = 0
test "$(uname -m)" = x86_64
id unityarpg >/dev/null
command -v bwrap >/dev/null
src=$(cd -- "$(dirname -- "$0")" && pwd)
version=4.5.16
archive=blender-$version-linux-x64
digest=23432b064977908beb1ab4b7f32f024e7331105c875c187dce002aff84b10c11
prefix=/opt/worldforge-blender
code=/usr/local/lib/worldforge-blender
store=/var/lib/unityarpg/.local/share/are-arpg/worldforge-blender
base=https://download.blender.org/release/Blender4.5
install -d -m 0755 "$prefix" "$code" /usr/local/bin
exec 9>"$prefix/.install.lock"
flock -x 9
marker="$prefix/.$archive.sha256"
if [[ ! -x "$prefix/$archive/blender" || ! -f "$marker" || $(cat "$marker") != "$digest" ]]; then
    tmp=$(mktemp -d "$prefix/.install.XXXXXX")
    trap 'rm -rf -- "$tmp"' EXIT
    curl --fail --silent --show-error --location --retry 3 --proto '=https' --proto-redir '=https' \
        -o "$tmp/$archive.tar.xz" "$base/$archive.tar.xz"
    (cd "$tmp" && printf '%s  %s\n' "$digest" "$archive.tar.xz" | sha256sum -c -)
    tar --no-same-owner -xJf "$tmp/$archive.tar.xz" -C "$tmp"
    if [[ -e "$prefix/$archive" ]]; then
        diff -qr --no-dereference "$tmp/$archive" "$prefix/$archive"
    else
        mv -T "$tmp/$archive" "$prefix/$archive"
    fi
    chown -hR root:root "$prefix/$archive"
    chmod -R go-w "$prefix/$archive"
    printf '%s\n' "$digest" > "$marker"
fi
runuser -u unityarpg -- "$prefix/$archive/blender" --version | head -n 1 | grep -Fx "Blender $version LTS"
if [[ ! -e /usr/local/bin/blender && ! -L /usr/local/bin/blender ]]; then
    ln -s "$prefix/$archive/blender" /usr/local/bin/blender
fi
if [[ (-e /usr/local/bin/worldforge-blender || -L /usr/local/bin/worldforge-blender) && \
      $(readlink -f /usr/local/bin/worldforge-blender) != "$code/worldforge_blender.py" ]]; then
    echo "Existing unrelated worldforge-blender command" >&2
    exit 1
fi
install -d -m 0700 -o unityarpg -g unityarpg "$store"
for file in blender_export.py worldforge_blender.py worldforge-blender.service worldforge-blender.timer; do
    test -s "$src/$file"
done
systemd-analyze verify "$src/worldforge-blender.timer"
# Finish any active conversion with the original recipe before swapping the scripts.
was_active=$(systemctl is-active worldforge-blender.timer || true)
restore_timer() {
    if [[ "$was_active" == active ]]; then systemctl start worldforge-blender.timer; fi
}
trap restore_timer ERR
systemctl stop worldforge-blender.timer 2>/dev/null || true
while systemctl is-active --quiet worldforge-blender.service || systemctl is-active worldforge-blender.service 2>/dev/null | grep -qx activating; do
    sleep 2
done
keep_recipe() {
    local recipe_version=$1 file=$2 hash destination
    [[ "$recipe_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || return 1
    hash=$(sha256sum "$file" | cut -d' ' -f1)
    install -d -m 0755 "$code/recipes/$recipe_version"
    destination="$code/recipes/$recipe_version/$hash.py"
    if [[ -e "$destination" ]]; then
        cmp "$file" "$destination"
    else
        install -m 0644 "$file" "$destination.new"
        mv -T "$destination.new" "$destination"
    fi
}
if [[ -f "$code/worldforge_blender.py" && -f "$code/blender_export.py" ]]; then
    previous=$(sed -n 's/^VERSION = "\([0-9.]*\)"$/\1/p' "$code/worldforge_blender.py")
    keep_recipe "$previous" "$code/blender_export.py"
fi
keep_recipe "$version" "$src/blender_export.py"
install -m 0644 "$src/blender_export.py" "$code/.blender_export.py.new"
mv -T "$code/.blender_export.py.new" "$code/blender_export.py"
install -m 0755 "$src/worldforge_blender.py" "$code/.worldforge_blender.py.new"
mv -T "$code/.worldforge_blender.py.new" "$code/worldforge_blender.py"
ln -sfnT "$code/worldforge_blender.py" /usr/local/bin/worldforge-blender
install -m 0644 "$src/worldforge-blender.service" "$src/worldforge-blender.timer" /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now worldforge-blender.timer
trap - ERR
