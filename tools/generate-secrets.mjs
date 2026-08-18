import { randomBytes } from "node:crypto";
const secret = (n = 32) => randomBytes(n).toString("hex");
console.log("ADMIN_TOKEN=" + secret());
console.log("LICENSE_PEPPER=" + secret());
