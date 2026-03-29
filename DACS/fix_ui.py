import os

create_path = r'Areas/QuanLyXNK/Views/KhoHang/Create.cshtml'
edit_path = r'Areas/QuanLyXNK/Views/KhoHang/Edit.cshtml'

old_address_block = """                        <div class="mb-3">
                            <label asp-for="DiaChi" class="form-label fw-semibold"></label>
                            <textarea asp-for="DiaChi" class="form-control form-control-sm" rows="3"></textarea>
                            <span asp-validation-for="DiaChi" class="text-danger d-block mt-1"></span>
                        </div>"""

new_address_block = """                        <div class="mb-3">
                            <label asp-for="DiaChi" class="form-label fw-semibold">Ð?a ch? Kho Hàng (Phân c?p) *</label>
                            
                            <div class="row mb-2">
                                <div class="col-md-4">
                                    <select id="tinhSelect" class="form-select form-select-sm">
                                        <option value="">-- Ch?n T?nh/Thành ph? --</option>
                                    </select>
                                </div>
                                <div class="col-md-4">
                                    <select id="huyenSelect" class="form-select form-select-sm" disabled>
                                        <option value="">-- Ch?n Qu?n/Huy?n --</option>
                                    </select>
                                </div>
                                <div class="col-md-4">
                                    <select id="xaSelect" class="form-select form-select-sm" disabled>
                                        <option value="">-- Ch?n Phu?ng/Xã --</option>
                                    </select>
                                </div>
                            </div>
                            <div class="row mb-2">
                                <div class="col-md-12">
                                    <input type="text" id="soNhaInput" class="form-control form-control-sm" placeholder="Nh?p s? nhà, tên du?ng... (s? t? d?ng d?nh v? trên b?n d?)" />
                                </div>
                            </div>

                            <label class="form-label fw-semibold mt-2">Ghim V? Trí Kho Hàng Trên B?n Ð?</label>
                            <!-- Search box for the map -->
                            <div class="input-group mb-2 d-none">
                                <input type="text" id="mapSearchBox" class="form-control form-control-sm" />
                                <button class="btn btn-outline-secondary btn-sm" type="button" id="btnSearchMap"><i class="fas fa-search"></i> Ð?nh v? l?i</button>
                            </div>
                            
                            <!-- Map Container -->
                            <div id="warehouseMap" style="height: 350px; width: 100%; border: 1px solid #ced4da; border-radius: 0.25rem; margin-bottom: 10px; z-index: 1;"></div>
                            
                            <textarea asp-for="DiaChi" id="DiaChi" class="form-control form-control-sm d-none" rows="2"></textarea>
                            <span asp-validation-for="DiaChi" class="text-danger d-block mt-1"></span>
                            
                            <!-- Hidden Coordinates Info -->
                            <input type="hidden" asp-for="Lat" id="Lat" />
                            <input type="hidden" asp-for="Lng" id="Lng" />
                            <small class="text-muted"><i class="fas fa-map-marker-alt text-danger"></i> T?a d? hi?n t?i: <span id="coordDisplay">Chua có</span></small> <span id="diachiTextInfo" class="text-primary fw-bold ms-2"></span>
                            <span asp-validation-for="Lat" class="text-danger d-block"></span>
                            <span asp-validation-for="Lng" class="text-danger d-block"></span>
                        </div>"""

script_block = """@section Scripts {
    @{
        await Html.RenderPartialAsync("_ValidationScriptsPartial");
    }
    
    <!-- Leaflet CSS & JS -->
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    
    <script>
        $(document).ready(function() {
            var latStr = $("#Lat").val();
            var lngStr = $("#Lng").val();
            var map, marker;
            
            var defaultLat = latStr ? parseFloat(latStr.replace(',','.')) : 10.762622;
            var defaultLng = lngStr ? parseFloat(lngStr.replace(',','.')) : 106.660172;
            if (isNaN(defaultLat) || defaultLat === 0) defaultLat = 10.762622;
            if (isNaN(defaultLng) || defaultLng === 0) defaultLng = 106.660172;

            map = L.map('warehouseMap').setView([defaultLat, defaultLng], 14);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '© OpenStreetMap'
            }).addTo(map);

            if (latStr && lngStr && defaultLat !== 10.762622) {
                 updateMarker(defaultLat, defaultLng);
                 map.setView([defaultLat, defaultLng], 15);
            }

            // Click b?n d?
            map.on('click', function(e) {
                var lat = e.latlng.lat;
                var lng = e.latlng.lng;
                updateMarker(lat, lng);
                reverseGeocode(lat, lng);
            });

            function updateMarker(lat, lng) {
                if (marker) map.removeLayer(marker);
                marker = L.marker([lat, lng]).addTo(map);
                $("#Lat").val(lat.toFixed(6));
                $("#Lng").val(lng.toFixed(6));
                $("#coordDisplay").text(lat.toFixed(6) + ", " + lng.toFixed(6));
            }

            function reverseGeocode(lat, lng) {
                var url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`;
                $.get(url, function(data) {
                    if(data && data.display_name) {
                        var parts = data.display_name.split(", ");
                        if(parts.length > 4) parts = parts.slice(0, parts.length - 1); // remove country
                        var finalAddress = parts.join(", ");
                        $("#DiaChi").val(finalAddress);
                        $("#diachiTextInfo").text(" - " + finalAddress);
                    }
                });
            }

            function searchMapAddress(query) {
                if(!query) return;
                var url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}`;
                $.get(url, function(data) {
                    if(data && data.length > 0) {
                        var lat = parseFloat(data[0].lat);
                        var lng = parseFloat(data[0].lon);
                        map.setView([lat, lng], 15);
                        updateMarker(lat, lng);
                    }
                });
            }

            setTimeout(function(){ map.invalidateSize(); }, 500);

            // Fetch T?nh Thành t? API esgoo.net (Free & Reliable for VN)
            $.getJSON('https://esgoo.net/api-tinhthanh/1/0.htm', function(data) {
                if(data.error === 0) {
                    $.each(data.data, function(key, val) {
                        $("#tinhSelect").append('<option value="' + val.id + '" data-name="' + val.full_name + '">' + val.full_name + '</option>');
                    });
                }
            });

            // Khi ch?n T?nh
            $("#tinhSelect").change(function() {
                var tinhId = $(this).val();
                $("#huyenSelect").html('<option value="">-- Ch?n Qu?n/Huy?n --</option>').prop('disabled', true);
                $("#xaSelect").html('<option value="">-- Ch?n Phu?ng/Xã --</option>').prop('disabled', true);
                updateFullAddress();
                
                if (tinhId) {
                    $.getJSON('https://esgoo.net/api-tinhthanh/2/' + tinhId + '.htm', function(data) {
                        if(data.error === 0) {
                            $("#huyenSelect").prop('disabled', false);
                            $.each(data.data, function(key, val) {
                                $("#huyenSelect").append('<option value="' + val.id + '" data-name="' + val.full_name + '">' + val.full_name + '</option>');
                            });
                        }
                    });
                    var tinhName = $("#tinhSelect option:selected").attr('data-name');
                    searchMapAddress(tinhName); // Bay t?i t?nh
                }
            });

            // Khi ch?n Huy?n
            $("#huyenSelect").change(function() {
                var huyenId = $(this).val();
                $("#xaSelect").html('<option value="">-- Ch?n Phu?ng/Xã --</option>').prop('disabled', true);
                updateFullAddress();
                
                if (huyenId) {
                    $.getJSON('https://esgoo.net/api-tinhthanh/3/' + huyenId + '.htm', function(data) {
                        if(data.error === 0) {
                            $("#xaSelect").prop('disabled', false);
                            $.each(data.data, function(key, val) {
                                $("#xaSelect").append('<option value="' + val.id + '" data-name="' + val.full_name + '">' + val.full_name + '</option>');
                            });
                        }
                    });
                    var huyenName = $("#huyenSelect option:selected").attr('data-name');
                    var tinhName = $("#tinhSelect option:selected").attr('data-name');
                    searchMapAddress(huyenName + ", " + tinhName);
                }
            });

            // Khi ch?n Xã
            $("#xaSelect").change(function() {
                updateFullAddress();
                var xaName = $("#xaSelect option:selected").attr('data-name');
                var huyenName = $("#huyenSelect option:selected").attr('data-name');
                var tinhName = $("#tinhSelect option:selected").attr('data-name');
                if(xaName && huyenName && tinhName) {
                    searchMapAddress(xaName + ", " + huyenName + ", " + tinhName);
                }
            });

            // Khi nh?p s? nhà
            let timeout = null;
            $("#soNhaInput").on('keyup', function() {
                clearTimeout(timeout);
                updateFullAddress();
                var soNha = $(this).val();
                var xaName = $("#xaSelect option:selected").attr('data-name');
                var huyenName = $("#huyenSelect option:selected").attr('data-name');
                var tinhName = $("#tinhSelect option:selected").attr('data-name');
                
                timeout = setTimeout(function () {
                    if(soNha && xaName && huyenName && tinhName) {
                        searchMapAddress(soNha + ", " + xaName + ", " + huyenName + ", " + tinhName);
                    }
                }, 1000); 
            });

            function updateFullAddress() {
                var tinhName = $("#tinhSelect option:selected").attr('data-name') || "";
                var huyenName = $("#huyenSelect option:selected").attr('data-name') || "";
                var xaName = $("#xaSelect option:selected").attr('data-name') || "";
                var soNha = $("#soNhaInput").val() || "";

                var addrParts = [];
                if(soNha) addrParts.push(soNha);
                if(xaName) addrParts.push(xaName);
                if(huyenName) addrParts.push(huyenName);
                if(tinhName) addrParts.push(tinhName);
                
                var fullAddress = addrParts.join(", ");
                $("#DiaChi").val(fullAddress);
                $("#diachiTextInfo").text(fullAddress ? (" - " + fullAddress) : "");
            }

        });
    </script>
}"""

import re

def process_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    if "warehouseMap" not in content:
        # replace old address
        content = content.replace(old_address_block, new_address_block)
        
        # replace scripts
        content = re.sub(r'@section Scripts\s*\{.*?\}', script_block, content, flags=re.DOTALL)
        
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Updated {path}")
    else:
        print(f"{path} already updated.")

process_file(create_path)
process_file(edit_path)

