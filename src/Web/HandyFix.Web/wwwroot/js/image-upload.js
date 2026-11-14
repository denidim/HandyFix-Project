document.addEventListener("DOMContentLoaded", function () {
    const fileInputs = document.querySelectorAll('input[type="file"][multiple]');
    
    fileInputs.forEach(input => {
        // Only run for inputs designed for compressed image upload
        if (!input.accept.includes("image/")) return;

        const maxFiles = parseInt(input.dataset.maxFiles || "5", 10);
        const maxSizeMb = parseFloat(input.dataset.maxSizeMb || "15");
        const maxSizeBytes = maxSizeMb * 1024 * 1024;
        // The most the picked photos may weigh together, where the page sets it (the admin's gallery).
        const maxTotalMb = parseFloat(input.dataset.maxTotalMb || "0");
        // How many picks are still being made smaller.
        let pending = 0;
        
        // Find container to render previews (either specified by ID or dynamically created)
        let previewGrid = document.querySelector(input.dataset.previewGrid);
        if (!previewGrid) {
            previewGrid = document.createElement("div");
            previewGrid.className = "image-preview-grid mt-3";
            input.parentNode.appendChild(previewGrid);
        }

        let containerCard = input.closest('.border-dashed-upload') || input.closest('.upload-dropzone');

        // Manage files state in memory via DataTransfer
        let activeDataTransfer = new DataTransfer();

        const updateInputAndPreviews = () => {
            input.files = activeDataTransfer.files;
            
            // Clean up previews
            previewGrid.innerHTML = "";
            
            // Re-render previews
            Array.from(activeDataTransfer.files).forEach((file, index) => {
                const item = document.createElement("div");
                item.className = "image-preview-item";
                
                const img = document.createElement("img");
                img.src = URL.createObjectURL(file);
                img.onload = () => URL.revokeObjectURL(img.src);
                
                const removeBtn = document.createElement("button");
                removeBtn.type = "button";
                removeBtn.className = "image-preview-remove";
                removeBtn.innerHTML = "&times;";
                removeBtn.title = "Remove image";
                removeBtn.addEventListener("click", (e) => {
                    e.stopPropagation();
                    const newDataTransfer = new DataTransfer();
                    Array.from(activeDataTransfer.files).forEach((f, i) => {
                        if (i !== index) newDataTransfer.items.add(f);
                    });
                    activeDataTransfer = newDataTransfer;
                    updateInputAndPreviews();
                });

                item.appendChild(img);
                item.appendChild(removeBtn);
                previewGrid.appendChild(item);
            });

            // Update dropzone UI text helper if it exists
            const uploadLabel = document.getElementById("uploadLabel") || document.querySelector(".upload-text");
            if (uploadLabel) {
                if (activeDataTransfer.files.length > 0) {
                    uploadLabel.innerText = `${activeDataTransfer.files.length} file(s) selected`;
                } else {
                    uploadLabel.innerText = containerCard.classList.contains('upload-dropzone') 
                        ? "Drag and drop images here or click to browse" 
                        : "Click to upload or drag and drop";
                }
            }
        };

        const processFiles = async (files) => {
            const validFiles = Array.from(files).filter(f => f.type.startsWith("image/"));
            
            if (activeDataTransfer.files.length + validFiles.length > maxFiles) {
                alert(`You can upload a maximum of ${maxFiles} images.`);
                return;
            }

            pending++;
            try {
                for (const file of validFiles) {
                    if (file.size > maxSizeBytes) {
                        alert(`File "${file.name}" exceeds the maximum allowed size of ${maxSizeMb}MB.`);
                        continue;
                    }

                    try {
                        const compressedFile = await compressImage(file);
                        activeDataTransfer.items.add(compressedFile);
                    } catch (err) {
                        console.error("Compression failed for file:", file.name, err);
                        // Fallback to original file if compression failed
                        activeDataTransfer.items.add(file);
                    }
                }
            } finally {
                pending--;
            }

            updateInputAndPreviews();
        };

        // The form is not sent while the picked photos are still being made smaller: it would
        // send the full-size originals, and a few phone photos are more than the server takes in
        // one request, which answered with a blank page (PROJECT_STATE Section 3fd). Nor is it
        // sent when the photos are heavier together than the page allows. This runs on the form,
        // before the one-click lock in site.js, which leaves a cancelled form alone.
        if (input.form) {
            input.form.addEventListener("submit", (e) => {
                if (pending > 0) {
                    e.preventDefault();
                    alert("Your photos are still being prepared. Please press the button again in a moment.");
                    return;
                }

                const totalBytes = Array.from(input.files).reduce((sum, f) => sum + f.size, 0);
                if (maxTotalMb > 0 && totalBytes > maxTotalMb * 1024 * 1024) {
                    e.preventDefault();
                    alert(`These photos are ${Math.ceil(totalBytes / 1048576)}MB together, more than the ${maxTotalMb}MB that can be sent at once. Please remove some and upload them in a second batch.`);
                }
            });
        }

        input.addEventListener("change", function (e) {
            if (this.files.length > 0) {
                processFiles(this.files);
            }
        });

        // Drag & Drop implementation
        if (containerCard) {
            containerCard.addEventListener("dragover", (e) => {
                e.preventDefault();
                containerCard.classList.add("dragover");
            });

            containerCard.addEventListener("dragleave", () => {
                containerCard.classList.remove("dragover");
            });

            containerCard.addEventListener("drop", (e) => {
                e.preventDefault();
                containerCard.classList.remove("dragover");
                if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                    processFiles(e.dataTransfer.files);
                }
            });
        }
    });
});

/**
 * Compresses an image file client-side using HTML5 Canvas
 * @param {File} file 
 * @returns {Promise<File>}
 */
function compressImage(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.readAsDataURL(file);
        reader.onload = (event) => {
            const img = new Image();
            img.src = event.target.result;
            img.onload = () => {
                const canvas = document.createElement("canvas");
                const MAX_WIDTH = 1920;
                let width = img.width;
                let height = img.height;

                if (width > MAX_WIDTH) {
                    height = Math.round((height * MAX_WIDTH) / width);
                    width = MAX_WIDTH;
                }

                canvas.width = width;
                canvas.height = height;

                const ctx = canvas.getContext("2d");
                ctx.drawImage(img, 0, 0, width, height);

                canvas.toBlob((blob) => {
                    if (!blob) {
                        reject(new Error("Canvas to Blob conversion failed"));
                        return;
                    }
                    const compressedFile = new File([blob], file.name.replace(/\.[^/.]+$/, "") + ".jpg", {
                        type: "image/jpeg",
                        lastModified: Date.now()
                    });
                    resolve(compressedFile);
                }, "image/jpeg", 0.8);
            };
            img.onerror = (err) => reject(err);
        };
        reader.onerror = (err) => reject(err);
    });
}
