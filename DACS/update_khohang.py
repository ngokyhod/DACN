import sys
import re

def update_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        text = f.read()

    # Address block replacement
    old_address_pattern = r'<label asp-for="DiaChi" class="form-label\s+fw-semibold"></label>\s*<textarea asp-for="DiaChi" class="form-control\s+form-control-sm" rows="3"></textarea>\s*<span asp-validation-for="DiaChi"\s+class="text-danger\s+d-block\s+mt-1"></span>'
    
    new_address = """<label asp-for="DiaChi" class="form-label fw-semibold">Ghim V? Trí Kho Hàng Trên B?n Ð? (Ho?c nh?p d?a ch? bên du?i) *</label>
                            
                            <!-- Search box for the map -->
                            <div class="input-group mb-2">
                                <input type="text" id="mapSearchBox" class="form-control form-control-sm" placeholder="Tìm ki?m d?a ch? trên b?n d?..." autocomplete="off" />
                                <button class="btn btn-outline-secondary btn-sm" type="button" id="btnSearchMap"><i class="fas fa-search"></i> Tìm</button>
                            </div>
                            
                            <!-- Map Container -->
                            <div id="warehouseMap" style="height: 350px; width: 100%; border: 1px solid #ced4da; border-radius: 0.25rem; margin-bottom: 10px; z-index: 1;"></div>
                            
                            <textarea asp-for="DiaChi" id="DiaChi" class="form-control form-control-sm" rows="2" placeholder="Ð?a ch? chi ti?t s? du?c t? d?ng di?n ho?c b?n có th? t? nh?p..."></textarea>
                            <span asp-validation-for="DiaChi" class="text-danger d-block mt-1"></span>
                            
                            <!-- Hidden Coordinates Info -->
                            <input type="hidden" asp-for="Lat" id="Lat" />
                            <input type="hidden" asp-for="Lng" id="Lng" />
                            <small class="text-muted"><i class="fas fa-map-marker-alt text-danger"></i> T?a d? hi?n t?i: <span id="coordDisplay">Chua có</span></small>
                            <span asp-validation-for="Lat" class="text-danger d-block"></span>
                            <span asp-validation-for="Lng" class="text-danger d-block"></span>"""

    text = re.sub(old_address_pattern, new_address, text, flags=re.DOTALL)

    script_part = """@section Scripts {
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
            
            var defaultLat = latStr ? parseFloat(latStr.replace(',','.')) : 10.762622;
            var defaultLng = lngStr ? parseFloat(lngStr.replace(',','.')) : 106.660172;
            
            if (isNaN(defaultLat) || defaultLat === 0) defaultLat = 10.762622;
            if (isNaN(defaultLng) || defaultLng === 0) defaultLng = 106.660172;

            var map = L.map('warehouseMap').setView([defaultLat, defaultLng], 14);
            
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '© OpenStreetMap'
            }).addTo(map);

            var marker;

            if (latStr && lngStr && defaultLat !== 10.762622) {
                 updateMarker(defaultLat, defaultLng);
                 // If not Ho Chi Minh default, center on it
                 map.setView([defaultLat, defaultLng], 15);
            }

            function onMapClick(e) {
                var lat = e.latlng.lat;
                var lng = e.latlng.lng;
                
                updateMarker(lat, lng);
                reverseGeocode(lat, lng);
            }

            map.on('click', onMapClick);

            function updateMarker(lat, lng) {
                if (marker) {
                    map.removeLayer(marker);
                }
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
                        // C?t b?t ph?n qu?c gia n?u quá dài, Nominatim hay tr? v? d?y d?
                        if(parts.length > 4) {
                            parts = parts.slice(0, parts.length - 1); // B? Qu?c gia
                        }
                        $("#DiaChi").val(parts.join(", "));
                    }
                });
            }

            $("#btnSearchMap").click(function() {
                var query = $("#mapSearchBox").val();
                if(query.trim() === "") return;
                
                var url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}`;
                $.get(url, function(data) {
                    if(data && data.length > 0) {
                        var lat = parseFloat(data[0].lat);
                        var lng = parseFloat(data[0].lon);
                        
                        map.setView([lat, lng], 15);
                        updateMarker(lat, lng);
                        var parts = data[0].display_name.split(", ");
                        if(parts.length > 4) {
                            parts = parts.slice(0, parts.length - 1);
                        }
                        $("#DiaChi").val(parts.join(", "));
                    } else {
                        alert("Không tìm th?y d?a ch? này trên b?n d?.");
                    }
                });
            });

            $("#mapSearchBox").keypress(function(e) {
                if(e.which == 13) {
                    e.preventDefault();
                    $("#btnSearchMap").click();
                }
            });
            
            setTimeout(function(){
                map.invalidateSize();
            }, 500);
        });
    </script>
}"""
    text = re.sub(r'@section Scripts\s*\{.*?\}', script_part, text, flags=re.DOTALL)

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(text)

update_file('Areas/QuanLyXNK/Views/KhoHang/Create.cshtml')
print('Updated!')
