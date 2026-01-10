Name:           lcdpossible
Version:        %{version}
Release:        1%{?dist}
Summary:        Cross-platform LCD controller service for HID-based displays

License:        MIT
URL:            https://github.com/DevPossible/LCDPossible
Source0:        lcdpossible-%{version}.tar.gz

BuildArch:      x86_64
Requires:       libicu

%description
LCDPossible is a cross-platform .NET service for controlling HID-based
LCD screens such as the Thermalright Trofeo Vision 360 ARGB. It provides
system monitoring displays, custom images, and slideshows.

%prep
%setup -q

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}/opt/lcdpossible
mkdir -p %{buildroot}/etc/systemd/system
mkdir -p %{buildroot}/etc/udev/rules.d
mkdir -p %{buildroot}/usr/local/bin

# Install application
cp -r * %{buildroot}/opt/lcdpossible/
chmod +x %{buildroot}/opt/lcdpossible/LCDPossible

# Install systemd service
cp %{buildroot}/opt/lcdpossible/lcdpossible.service %{buildroot}/etc/systemd/system/

# Install udev rules
cp %{buildroot}/opt/lcdpossible/99-lcdpossible.rules %{buildroot}/etc/udev/rules.d/

# Create symlink
ln -s /opt/lcdpossible/LCDPossible %{buildroot}/usr/local/bin/lcdpossible

%post
# Reload udev rules
udevadm control --reload-rules || true
udevadm trigger || true
# Reload systemd
systemctl daemon-reload || true

%preun
# Stop service if running
if systemctl is-active --quiet lcdpossible; then
    systemctl stop lcdpossible || true
fi
if systemctl is-enabled --quiet lcdpossible; then
    systemctl disable lcdpossible || true
fi

%postun
# Reload systemd
systemctl daemon-reload || true
# Reload udev rules
udevadm control --reload-rules || true

%files
/opt/lcdpossible
/etc/systemd/system/lcdpossible.service
/etc/udev/rules.d/99-lcdpossible.rules
/usr/local/bin/lcdpossible

%changelog
