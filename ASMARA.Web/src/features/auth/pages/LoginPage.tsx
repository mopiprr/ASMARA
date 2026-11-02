
import LoginForm from '../components/LoginForm';

function LoginPage() {
  return (
    <main className="relative flex min-h-screen items-center justify-center overflow-hidden p-4 md:p-8 bg-gray-950">

      {/* BACKGROUND */}
      <div
        className="absolute inset-0 scale-105"
        style={{
          backgroundImage: "url('/images/asmara-background.png')",
          backgroundSize: 'cover',
          backgroundPosition: 'center',
          backgroundRepeat: 'no-repeat',
        }}
      />

      {/* OVERLAY */}
      <div className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" />

      {/* LOGIN PANEL */}
      <section className="relative z-10 w-full max-w-[700px] rounded-[40px] border border-white/40 bg-white/95 p-12 shadow-[0_20px_50px_rgba(0,0,0,0.3)] backdrop-blur-2xl sm:p-16">

        {/* LOGO */}
        <div className="mb-10 flex justify-center">
          <img
            src="/images/asmara-logo.png"
            alt="ASMARA - Assets Manajemen Sarana"
            className="h-auto w-80 object-contain drop-shadow-sm"
          />
        </div>

        {/* HEADING */}
        <div className="mb-10 text-center">
          <h1 className="text-4xl font-extrabold tracking-tight text-gray-900">
            Welcome Back!
          </h1>

          <p className="mt-4 text-base leading-relaxed text-gray-600">
            Silakan masuk menggunakan akun perusahaan
            <br />
            untuk mengakses sistem ASMARA.
          </p>
        </div>

        {/* FORM */}
        <div className="[&_input]:rounded-xl [&_input]:border-gray-200 [&_input]:bg-gray-50/50 [&_input]:py-5 [&_input]:text-lg [&_input]:transition-all [&_input:focus]:border-blue-600 [&_input:focus]:bg-white [&_input:focus]:ring-2 [&_input:focus]:ring-blue-100 [&_button]:rounded-xl [&_button]:bg-transparent-600 [&_button]:py-5 [&_button]:text-lg [&_button]:transition-all [&_button:hover]:bg-transparent-700">
          <LoginForm />
        </div>

        {/* FOOTER */}
        <div className="mt-10 border-t border-gray-100 pt-7 text-center">
          <p className="text-base font-semibold text-gray-700">
            ASMARA - Assets Manajemen Sarana
          </p>

          <p className="mt-2 text-sm text-gray-400">
            © {new Date().getFullYear()} Mitra Integrasi Informatika
          </p>
        </div>

      </section>
    </main>
  );
}

export default LoginPage;
