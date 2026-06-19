window.digitalSigningCrypto = {
    async generateKeyPair() {
        const keyPair = await crypto.subtle.generateKey(
            {
                name: "RSA-PSS",
                modulusLength: 2048,
                publicExponent: new Uint8Array([1, 0, 1]),
                hash: "SHA-256"
            },
            true,
            ["sign", "verify"]);

        const publicKey = await crypto.subtle.exportKey("spki", keyPair.publicKey);
        const privateKey = await crypto.subtle.exportKey("pkcs8", keyPair.privateKey);
        const publicPem = toPem("PUBLIC KEY", publicKey);
        const privatePem = toPem("PRIVATE KEY", privateKey);

        downloadText("digital-signing-private-key.pem", privatePem);
        return publicPem;
    },

    async signPayload(privateKeyPem, payload) {
        const privateKey = await crypto.subtle.importKey(
            "pkcs8",
            fromPem(privateKeyPem),
            { name: "RSA-PSS", hash: "SHA-256" },
            false,
            ["sign"]);

        const signature = await crypto.subtle.sign(
            { name: "RSA-PSS", saltLength: 32 },
            privateKey,
            new TextEncoder().encode(payload));

        return arrayBufferToBase64(signature);
    }
};

function toPem(label, buffer) {
    const base64 = arrayBufferToBase64(buffer);
    const lines = base64.match(/.{1,64}/g) ?? [];
    return `-----BEGIN ${label}-----\n${lines.join("\n")}\n-----END ${label}-----`;
}

function fromPem(pem) {
    const base64 = pem
        .replace(/-----BEGIN [^-]+-----/g, "")
        .replace(/-----END [^-]+-----/g, "")
        .replace(/\s+/g, "");

    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) {
        bytes[index] = binary.charCodeAt(index);
    }

    return bytes.buffer;
}

function arrayBufferToBase64(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = "";
    for (const byte of bytes) {
        binary += String.fromCharCode(byte);
    }

    return btoa(binary);
}

function downloadText(fileName, text) {
    const blob = new Blob([text], { type: "text/plain" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}
